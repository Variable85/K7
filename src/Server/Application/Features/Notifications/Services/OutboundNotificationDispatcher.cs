using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Services;

public class OutboundNotificationDispatcher(
    IApplicationDbContext context,
    IServiceProvider serviceProvider,
    NotificationConditionEvaluator conditionEvaluator,
    NotificationPayloadRenderer payloadRenderer,
    NotificationEventEnricher enricher,
    OutboundNotificationBatcher batcher,
    ILogger<OutboundNotificationDispatcher> logger)
{
    private static readonly HashSet<string> BatchableEvents = new(StringComparer.Ordinal)
    {
        nameof(MediaAddedEvent),
        nameof(MediaCreatedEvent)
    };

    private static readonly HashSet<string> BatchableMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(MediaType.SerieEpisode),
        nameof(MediaType.SerieSeason),
        nameof(MediaType.MusicTrack),
        nameof(MediaType.MusicAlbum)
    };

    public async Task DispatchAsync(
        string eventTypeName,
        IReadOnlyDictionary<string, object?> eventData,
        Domain.Common.BaseEvent? domainEvent,
        CancellationToken cancellationToken)
    {
        var rules = await context.NotificationRules
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        var matchingRules = rules.Where(r => r.EventTypeNames.Contains(eventTypeName)).ToList();
        if (matchingRules.Count == 0)
            return;

        var enrichedData = domainEvent is not null
            ? await enricher.EnrichAsync(domainEvent, eventData, cancellationToken)
            : enricher.EnrichWithGlobals(eventData);

        foreach (var rule in matchingRules)
        {
            try
            {
                await ProcessRuleAsync(rule, eventTypeName, enrichedData, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process notification rule {RuleId} ({RuleName})", rule.Id, rule.Name);
            }
        }
    }

    public async Task SendBatchedAsync(
        Guid ruleId,
        NotificationProviderType providerType,
        string providerConfig,
        NotificationPayloadFormat payloadFormat,
        string? titleTemplate,
        string? bodyTemplate,
        string? rawJsonTemplate,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return;

        var serieIds = items
            .Select(i => i.TryGetValue("Serie.Id", out var v) ? v?.ToString() : null)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct();

        var seasonCounts = await SerieSeasonCountHelper.GetCountsBySerieIdsAsync(context, serieIds, cancellationToken);
        var seasonCountsByString = seasonCounts.ToDictionary(
            kv => kv.Key.ToString(),
            kv => kv.Value,
            StringComparer.OrdinalIgnoreCase);

        var groups = NotificationMediaBatchGrouper.Aggregate(items, seasonCountsByString);
        var provider = serviceProvider.GetRequiredKeyedService<INotificationProvider>(providerType);

        foreach (var groupData in groups)
        {
            var ruleSnapshot = new NotificationRule
            {
                Id = ruleId,
                Name = "batch",
                IsEnabled = true,
                ProviderType = providerType,
                PayloadFormat = payloadFormat,
                ProviderConfig = providerConfig,
                TitleTemplate = titleTemplate,
                BodyTemplate = bodyTemplate,
                RawJsonTemplate = rawJsonTemplate,
                EventTypeNames = []
            };

            var payload = BuildPayload(ruleSnapshot, groupData);
            var success = await provider.SendAsync(providerConfig, payload, cancellationToken);
            if (success)
            {
                logger.LogDebug("Batched notification sent for rule {RuleId}", ruleId);
                await context.NotificationRules
                    .Where(r => r.Id == ruleId)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSentAt, DateTimeOffset.UtcNow), cancellationToken);
            }
            else
            {
                logger.LogError("Batched notification delivery failed for rule {RuleId}", ruleId);
            }
        }
    }

    private async Task ProcessRuleAsync(
        NotificationRule rule,
        string eventTypeName,
        IReadOnlyDictionary<string, object?> eventData,
        CancellationToken cancellationToken)
    {
        if (!NotificationScheduleEvaluator.IsWithinWindow(rule.ScheduleWindows, DateTimeOffset.Now))
        {
            logger.LogDebug("Notification rule {RuleId} outside schedule window, skipping", rule.Id);
            return;
        }

        if (rule.CooldownSeconds is > 0 && rule.LastSentAt is DateTimeOffset lastSent)
        {
            var elapsed = DateTimeOffset.UtcNow - lastSent;
            if (elapsed < TimeSpan.FromSeconds(rule.CooldownSeconds.Value))
            {
                logger.LogDebug("Notification rule {RuleId} in cooldown, skipping", rule.Id);
                return;
            }
        }

        if (!conditionEvaluator.Evaluate(rule.RuleFilter, eventData))
        {
            logger.LogDebug("Notification rule {RuleId} conditions not met, skipping", rule.Id);
            return;
        }

        if (rule.BatchDebounceSeconds is > 0
            && BatchableEvents.Contains(eventTypeName)
            && IsBatchableMedia(eventData))
        {
            batcher.Enqueue(
                rule.Id,
                rule.BatchDebounceSeconds.Value,
                rule.ProviderType,
                rule.ProviderConfig,
                rule.PayloadFormat,
                rule.TitleTemplate,
                rule.BodyTemplate,
                rule.RawJsonTemplate,
                eventData);
            return;
        }

        var payload = BuildPayload(rule, eventData);
        var provider = serviceProvider.GetRequiredKeyedService<INotificationProvider>(rule.ProviderType);
        var success = await provider.SendAsync(rule.ProviderConfig, payload, cancellationToken);

        if (success)
        {
            logger.LogDebug("Notification sent for rule {RuleId} ({RuleName}) via {ProviderType}",
                rule.Id, rule.Name, rule.ProviderType);

            await context.NotificationRules
                .Where(r => r.Id == rule.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LastSentAt, DateTimeOffset.UtcNow), cancellationToken);
        }
        else
        {
            logger.LogError("Notification delivery failed for rule {RuleId} ({RuleName}) via {ProviderType}",
                rule.Id, rule.Name, rule.ProviderType);
        }
    }

    private static bool IsBatchableMedia(IReadOnlyDictionary<string, object?> eventData)
    {
        if (!eventData.TryGetValue("Media.Type", out var raw) || raw is null)
            return false;

        return BatchableMediaTypes.Contains(raw.ToString() ?? "");
    }

    private string BuildPayload(NotificationRule rule, IReadOnlyDictionary<string, object?> eventData)
    {
        if (rule.PayloadFormat == NotificationPayloadFormat.RawJson)
            return payloadRenderer.Render(rule.RawJsonTemplate, eventData);

        var title = payloadRenderer.RenderPlain(rule.TitleTemplate, eventData);
        var body = payloadRenderer.RenderPlain(rule.BodyTemplate, eventData);
        return JsonSerializer.Serialize(new { title, body }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
