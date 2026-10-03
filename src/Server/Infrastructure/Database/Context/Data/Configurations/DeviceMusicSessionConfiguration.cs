using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class DeviceMusicSessionConfiguration : IEntityTypeConfiguration<DeviceMusicSession>
{
    public void Configure(EntityTypeBuilder<DeviceMusicSession> builder)
    {
        builder.HasIndex(e => new { e.UserId, e.DeviceId })
            .IsUnique()
            .HasFilter("\"SharedProfileId\" IS NULL")
            .HasDatabaseName("UX_DeviceMusicSessions_User_Device_Personal");

        builder.HasIndex(e => new { e.UserId, e.DeviceId, e.SharedProfileId })
            .IsUnique()
            .HasFilter("\"SharedProfileId\" IS NOT NULL")
            .HasDatabaseName("UX_DeviceMusicSessions_User_Device_Profile");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Device)
            .WithMany()
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SharedProfile)
            .WithMany()
            .HasForeignKey(e => e.SharedProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
