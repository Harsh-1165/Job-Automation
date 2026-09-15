using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobAutomation.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250915180000_Phase8Observability")]
public partial class Phase8Observability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAdmin",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "worker_heartbeats",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                HostName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastHeartbeatAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_worker_heartbeats", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_worker_heartbeats_LastHeartbeatAtUtc",
            table: "worker_heartbeats",
            column: "LastHeartbeatAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_worker_heartbeats_WorkerId",
            table: "worker_heartbeats",
            column: "WorkerId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "worker_heartbeats");

        migrationBuilder.DropColumn(
            name: "IsAdmin",
            table: "users");
    }
}
