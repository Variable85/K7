using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceMusicSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceMusicSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RadioJson = table.Column<string>(type: "text", nullable: true),
                    CurrentMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentIndexedFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentIndex = table.Column<int>(type: "integer", nullable: false),
                    PositionSeconds = table.Column<double>(type: "double precision", nullable: false),
                    RepeatMode = table.Column<int>(type: "integer", nullable: false),
                    Shuffle = table.Column<bool>(type: "boolean", nullable: false),
                    ShuffleSeed = table.Column<int>(type: "integer", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceMusicSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceMusicSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceMusicSessions_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeviceMusicSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMusicSessions_DeviceId",
                table: "DeviceMusicSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMusicSessions_SharedProfileId",
                table: "DeviceMusicSessions",
                column: "SharedProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_DeviceMusicSessions_User_Device_Personal",
                table: "DeviceMusicSessions",
                columns: new[] { "UserId", "DeviceId" },
                unique: true,
                filter: "\"SharedProfileId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DeviceMusicSessions_User_Device_Profile",
                table: "DeviceMusicSessions",
                columns: new[] { "UserId", "DeviceId", "SharedProfileId" },
                unique: true,
                filter: "\"SharedProfileId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceMusicSessions");
        }
    }
}
