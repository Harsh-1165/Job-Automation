using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobAutomation.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250914180000_Phase2AuthAndJobs")]
public partial class Phase2AuthAndJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_users_Email",
            table: "users");

        migrationBuilder.AddColumn<string>(
            name: "NormalizedEmail",
            table: "users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql(
            "UPDATE users SET \"NormalizedEmail\" = LOWER(\"Email\") WHERE \"NormalizedEmail\" = ''");

        migrationBuilder.AddColumn<DateTime>(
            name: "LastRunAtUtc",
            table: "jobs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_NormalizedEmail",
            table: "users",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.DropForeignKey(
            name: "FK_jobs_users_UserId",
            table: "jobs");

        migrationBuilder.AddForeignKey(
            name: "FK_jobs_users_UserId",
            table: "jobs",
            column: "UserId",
            principalTable: "users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_jobs_users_UserId",
            table: "jobs");

        migrationBuilder.DropIndex(
            name: "IX_users_NormalizedEmail",
            table: "users");

        migrationBuilder.DropColumn(
            name: "NormalizedEmail",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LastRunAtUtc",
            table: "jobs");

        migrationBuilder.CreateIndex(
            name: "IX_users_Email",
            table: "users",
            column: "Email",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_jobs_users_UserId",
            table: "jobs",
            column: "UserId",
            principalTable: "users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
