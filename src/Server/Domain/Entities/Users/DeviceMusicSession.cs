using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Users;

public class DeviceMusicSession : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public Guid? SharedProfileId { get; set; }
    public SharedProfile? SharedProfile { get; set; }

    public MusicSessionSourceKind SourceKind { get; set; }
    public Guid? SourceId { get; set; }
    public string? RadioJson { get; set; }

    public Guid? CurrentMediaId { get; set; }
    public Guid? CurrentIndexedFileId { get; set; }
    public int CurrentIndex { get; set; }
    public double PositionSeconds { get; set; }
    public int RepeatMode { get; set; }
    public bool Shuffle { get; set; }
    public int ShuffleSeed { get; set; }

    public string ItemsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; }
}
