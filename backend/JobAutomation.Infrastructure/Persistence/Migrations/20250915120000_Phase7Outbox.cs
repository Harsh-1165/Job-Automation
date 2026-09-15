using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobAutomation.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250915120000_Phase7Outbox")]
public partial class Phase7Outbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ManualRetryIdempotencyKey",
            table: "executions",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MessageType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                ExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LockId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_executions_Status_NextRetryAtUtc",
            table: "executions",
            columns: new[] { "Status", "NextRetryAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_CreatedAtUtc",
            table: "outbox_messages",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_ExecutionId_MessageType_Status",
            table: "outbox_messages",
            columns: new[] { "ExecutionId", "MessageType", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_Status_LockedUntilUtc",
            table: "outbox_messages",
            columns: new[] { "Status", "LockedUntilUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_Status_NextAttemptAtUtc",
            table: "outbox_messages",
            columns: new[] { "Status", "NextAttemptAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_executions_Status_NextRetryAtUtc",
            table: "executions");

        migrationBuilder.DropColumn(
            name: "ManualRetryIdempotencyKey",
            table: "executions");
    }
}
