using System.Collections.Concurrent;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Notifications.Services;

public sealed class OutboundNotificationBatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboundNotificationBatcher> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, RuleBatchState> _batches = new();
    private const int MaxBatchSize = 100;

    public void Enqueue(
        Guid ruleId,
        int debounceSeconds,
        NotificationProviderType providerType,
        string providerConfig,
        NotificationPayloadFormat payloadFormat,
        string? titleTemplate,
        string? bodyTemplate,
        string? rawJsonTemplate,
        IReadOnlyDictionary<string, object?> eventData)
    {
        var state = _batches.GetOrAdd(ruleId, id => new RuleBatchState(this, id));
        lock (state.Sync)
        {
            var wasEmpty = state.Items.Count == 0;
            state.DebounceSeconds = Math.Max(1, debounceSeconds);
            state.ProviderType = providerType;
            state.ProviderConfig = providerConfig;
            state.PayloadFormat = payloadFormat;
            state.TitleTemplate = titleTemplate;
            state.BodyTemplate = bodyTemplate;
            state.RawJsonTemplate = rawJsonTemplate;
            state.Items.Add(new Dictionary<string, object?>(eventData, StringComparer.OrdinalIgnoreCase));

            state.IdleTimer.Change(TimeSpan.FromSeconds(state.DebounceSeconds), Timeout.InfiniteTimeSpan);

            if (wasEmpty)
                state.MaxWaitTimer.Change(MaxWaitFor(state.DebounceSeconds), Timeout.InfiniteTimeSpan);

            if (state.Items.Count >= MaxBatchSize)
                _ = FlushAsync(ruleId);
        }
    }

    private static TimeSpan MaxWaitFor(int debounceSeconds) =>
        TimeSpan.FromSeconds(Math.Min(Math.Max(debounceSeconds * 6, 30), 300));

    private async Task FlushAsync(Guid ruleId)
    {
        if (!_batches.TryGetValue(ruleId, out var state))
            return;

        List<IReadOnlyDictionary<string, object?>> items;
        NotificationProviderType providerType;
        string providerConfig;
        NotificationPayloadFormat payloadFormat;
        string? titleTemplate;
        string? bodyTemplate;
        string? rawJsonTemplate;

        lock (state.Sync)
        {
            if (state.Items.Count == 0 || state.Flushing)
                return;

            state.Flushing = true;
            items = state.Items.Cast<IReadOnlyDictionary<string, object?>>().ToList();
            state.Items.Clear();
            state.IdleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            state.MaxWaitTimer.Change(Timeout.Infinite, Timeout.Infinite);
            providerType = state.ProviderType;
            providerConfig = state.ProviderConfig;
            payloadFormat = state.PayloadFormat;
            titleTemplate = state.TitleTemplate;
            bodyTemplate = state.BodyTemplate;
            rawJsonTemplate = state.RawJsonTemplate;
        }

        try
        {
            logger.LogDebug("Flushing {Count} batched outbound notifications for rule {RuleId}", items.Count, ruleId);
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboundNotificationDispatcher>();
            await dispatcher.SendBatchedAsync(
                ruleId,
                providerType,
                providerConfig,
                payloadFormat,
                titleTemplate,
                bodyTemplate,
                rawJsonTemplate,
                items,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to flush outbound notification batch for rule {RuleId}", ruleId);
        }
        finally
        {
            lock (state.Sync)
            {
                state.Flushing = false;
                if (state.Items.Count > 0)
                {
                    state.IdleTimer.Change(TimeSpan.FromSeconds(state.DebounceSeconds), Timeout.InfiniteTimeSpan);
                    state.MaxWaitTimer.Change(MaxWaitFor(state.DebounceSeconds), Timeout.InfiniteTimeSpan);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var ruleId in _batches.Keys.ToList())
            await FlushAsync(ruleId);

        foreach (var state in _batches.Values)
        {
            await state.IdleTimer.DisposeAsync();
            await state.MaxWaitTimer.DisposeAsync();
        }

        _batches.Clear();
    }

    private sealed class RuleBatchState
    {
        public RuleBatchState(OutboundNotificationBatcher owner, Guid ruleId)
        {
            IdleTimer = new Timer(_ => _ = owner.FlushAsync(ruleId), null, Timeout.Infinite, Timeout.Infinite);
            MaxWaitTimer = new Timer(_ => _ = owner.FlushAsync(ruleId), null, Timeout.Infinite, Timeout.Infinite);
        }

        public object Sync { get; } = new();
        public List<Dictionary<string, object?>> Items { get; } = [];
        public bool Flushing { get; set; }
        public int DebounceSeconds { get; set; } = 5;
        public NotificationProviderType ProviderType { get; set; }
        public string ProviderConfig { get; set; } = "";
        public NotificationPayloadFormat PayloadFormat { get; set; }
        public string? TitleTemplate { get; set; }
        public string? BodyTemplate { get; set; }
        public string? RawJsonTemplate { get; set; }
        public Timer IdleTimer { get; }
        public Timer MaxWaitTimer { get; }
    }
}
