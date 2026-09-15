using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobAutomation.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250914200000_Phase4Concurrency")]
public partial class Phase4Concurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastHeartbeatAtUtc",
            table: "executions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LeaseId",
            table: "executions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LeaseExpiresAtUtc",
            table: "executions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_executions_JobId_CreatedAtUtc",
            table: "executions",
            columns: new[] { "JobId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_executions_Status_CreatedAtUtc",
            table: "executions",
            columns: new[] { "Status", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_executions_Status_LeaseExpiresAtUtc",
            table: "executions",
            columns: new[] { "Status", "LeaseExpiresAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_executions_Status_LeaseExpiresAtUtc",
            table: "executions");

        migrationBuilder.DropIndex(
            name: "IX_executions_Status_CreatedAtUtc",
            table: "executions");

        migrationBuilder.DropIndex(
            name: "IX_executions_JobId_CreatedAtUtc",
            table: "executions");

        migrationBuilder.DropColumn(
            name: "LastHeartbeatAtUtc",
            table: "executions");

        migrationBuilder.DropColumn(
            name: "LeaseId",
            table: "executions");

        migrationBuilder.DropColumn(
            name: "LeaseExpiresAtUtc",
            table: "executions");
    }
}
