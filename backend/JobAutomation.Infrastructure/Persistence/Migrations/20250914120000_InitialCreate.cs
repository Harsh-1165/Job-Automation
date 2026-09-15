using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobAutomation.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250914120000_InitialCreate")]
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                TargetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                RequestHeadersJson = table.Column<string>(type: "text", nullable: true),
                RequestBody = table.Column<string>(type: "text", nullable: true),
                CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                NextRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                MaxRetries = table.Column<int>(type: "integer", nullable: false),
                TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_jobs_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "executions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                TriggerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                ResponseBody = table.Column<string>(type: "text", nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextRetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_executions", x => x.Id);
                table.ForeignKey(
                    name: "FK_executions_jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "execution_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Details = table.Column<string>(type: "text", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_execution_logs_executions_ExecutionId",
                    column: x => x.ExecutionId,
                    principalTable: "executions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_execution_logs_CreatedAtUtc",
            table: "execution_logs",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_execution_logs_ExecutionId",
            table: "execution_logs",
            column: "ExecutionId");

        migrationBuilder.CreateIndex(
            name: "IX_executions_CreatedAtUtc",
            table: "executions",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_executions_JobId",
            table: "executions",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_executions_JobId_IdempotencyKey",
            table: "executions",
            columns: new[] { "JobId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_executions_Status",
            table: "executions",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_jobs_NextRunAtUtc",
            table: "jobs",
            column: "NextRunAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_jobs_Status",
            table: "jobs",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_jobs_UserId",
            table: "jobs",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_users_Email",
            table: "users",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "execution_logs");
        migrationBuilder.DropTable(name: "executions");
        migrationBuilder.DropTable(name: "jobs");
        migrationBuilder.DropTable(name: "users");
    }
}
