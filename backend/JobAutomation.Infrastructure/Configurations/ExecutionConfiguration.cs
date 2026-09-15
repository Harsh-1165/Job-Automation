using JobAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobAutomation.Infrastructure.Configurations;

public class ExecutionConfiguration : IEntityTypeConfiguration<Execution>
{
    public void Configure(EntityTypeBuilder<Execution> builder)
    {
        builder.ToTable("executions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.TriggerType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ManualRetryIdempotencyKey)
            .HasMaxLength(256);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();

        builder.Property(e => e.LeaseId)
            .HasColumnType("uuid");

        builder.HasOne(e => e.Job)
            .WithMany(j => j.Executions)
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.JobId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAtUtc);

        builder.HasIndex(e => new { e.JobId, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.Status, e.LeaseExpiresAtUtc });

        builder.HasIndex(e => new { e.Status, e.NextRetryAtUtc });

        builder.HasIndex(e => new { e.JobId, e.IdempotencyKey })
            .IsUnique();
    }
}
