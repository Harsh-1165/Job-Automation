using System;
using JobAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JobAutomation.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("JobAutomation.Domain.Entities.Execution", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<int>("AttemptNumber")
                    .HasColumnType("integer");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("ErrorMessage")
                    .HasMaxLength(4000)
                    .HasColumnType("character varying(4000)");

                b.Property<string>("IdempotencyKey")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)");

                b.Property<string>("ManualRetryIdempotencyKey")
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)");

                b.Property<Guid>("JobId")
                    .HasColumnType("uuid");

                b.Property<DateTime?>("NextRetryAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("ResponseBody")
                    .HasColumnType("text");

                b.Property<DateTime?>("StartedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)");

                b.Property<string>("TriggerType")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)");

                b.Property<int?>("HttpStatusCode")
                    .HasColumnType("integer");

                b.Property<DateTime?>("LastHeartbeatAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<Guid?>("LeaseId")
                    .HasColumnType("uuid");

                b.Property<DateTime?>("LeaseExpiresAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTime>("UpdatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.HasKey("Id");

                b.HasIndex("CreatedAtUtc");

                b.HasIndex("JobId");

                b.HasIndex("Status");

                b.HasIndex("JobId", "CreatedAtUtc");

                b.HasIndex("Status", "CreatedAtUtc");

                b.HasIndex("Status", "LeaseExpiresAtUtc");

                b.HasIndex("Status", "NextRetryAtUtc");

                b.HasIndex("JobId", "IdempotencyKey")
                    .IsUnique();

                b.ToTable("executions", (string)null);
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.OutboxMessage", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<int>("AttemptCount")
                    .HasColumnType("integer");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<Guid?>("ExecutionId")
                    .HasColumnType("uuid");

                b.Property<string>("LastError")
                    .HasMaxLength(4000)
                    .HasColumnType("character varying(4000)");

                b.Property<Guid?>("LockId")
                    .HasColumnType("uuid");

                b.Property<DateTime?>("LockedUntilUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("MessageType")
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)");

                b.Property<DateTime?>("NextAttemptAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Payload")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<DateTime?>("ProcessedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)");

                b.HasKey("Id");

                b.HasIndex("CreatedAtUtc");

                b.HasIndex("ExecutionId", "MessageType", "Status");

                b.HasIndex("Status", "LockedUntilUtc");

                b.HasIndex("Status", "NextAttemptAtUtc");

                b.ToTable("outbox_messages", (string)null);
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.ExecutionLog", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Details")
                    .HasColumnType("text");

                b.Property<Guid>("ExecutionId")
                    .HasColumnType("uuid");

                b.Property<string>("Level")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<string>("Message")
                    .IsRequired()
                    .HasMaxLength(4000)
                    .HasColumnType("character varying(4000)");

                b.HasKey("Id");

                b.HasIndex("CreatedAtUtc");

                b.HasIndex("ExecutionId");

                b.ToTable("execution_logs", (string)null);
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.Job", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("CronExpression")
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("Description")
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.Property<string>("HttpMethod")
                    .IsRequired()
                    .HasMaxLength(10)
                    .HasColumnType("character varying(10)");

                b.Property<int>("MaxRetries")
                    .HasColumnType("integer");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<DateTime?>("LastRunAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTime?>("NextRunAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("RequestBody")
                    .HasColumnType("text");

                b.Property<string>("RequestHeadersJson")
                    .HasColumnType("text");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("character varying(32)");

                b.Property<string>("TargetUrl")
                    .IsRequired()
                    .HasMaxLength(2048)
                    .HasColumnType("character varying(2048)");

                b.Property<int>("TimeoutSeconds")
                    .HasColumnType("integer");

                b.Property<DateTime>("UpdatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("NextRunAtUtc");

                b.HasIndex("Status");

                b.HasIndex("UserId");

                b.ToTable("jobs", (string)null);
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.User", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("DisplayName")
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<string>("Email")
                    .IsRequired()
                    .HasMaxLength(320)
                    .HasColumnType("character varying(320)");

                b.Property<string>("NormalizedEmail")
                    .IsRequired()
                    .HasMaxLength(320)
                    .HasColumnType("character varying(320)");

                b.Property<string>("PasswordHash")
                    .IsRequired()
                    .HasMaxLength(512)
                    .HasColumnType("character varying(512)");

                b.Property<bool>("IsAdmin")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                b.Property<DateTime>("UpdatedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.HasKey("Id");

                b.HasIndex("NormalizedEmail")
                    .IsUnique();

                b.ToTable("users", (string)null);
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.WorkerHeartbeat", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("HostName")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)");

                b.Property<DateTime>("LastHeartbeatAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTime?>("LastProcessedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<DateTime>("StartedAtUtc")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("WorkerId")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)");

                b.HasKey("Id");

                b.HasIndex("LastHeartbeatAtUtc");

                b.HasIndex("WorkerId")
                    .IsUnique();

                b.ToTable("worker_heartbeats", (string)null);
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.Execution", b =>
            {
                b.HasOne("JobAutomation.Domain.Entities.Job", "Job")
                    .WithMany("Executions")
                    .HasForeignKey("JobId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Job");
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.ExecutionLog", b =>
            {
                b.HasOne("JobAutomation.Domain.Entities.Execution", "Execution")
                    .WithMany("Logs")
                    .HasForeignKey("ExecutionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Execution");
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.Job", b =>
            {
                b.HasOne("JobAutomation.Domain.Entities.User", "User")
                    .WithMany("Jobs")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.Navigation("User");
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.Execution", b =>
            {
                b.Navigation("Logs");
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.Job", b =>
            {
                b.Navigation("Executions");
            });

        modelBuilder.Entity("JobAutomation.Domain.Entities.User", b =>
            {
                b.Navigation("Jobs");
            });
#pragma warning restore 612, 618
    }
}
