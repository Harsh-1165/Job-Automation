using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobAutomation.Infrastructure.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(m => m.LastError)
            .HasMaxLength(4000);

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.Status, m.NextAttemptAtUtc });
        builder.HasIndex(m => new { m.Status, m.LockedUntilUtc });
        builder.HasIndex(m => m.CreatedAtUtc);
        builder.HasIndex(m => new { m.ExecutionId, m.MessageType, m.Status });
    }
}
