using JobAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobAutomation.Infrastructure.Configurations;

public class ExecutionLogConfiguration : IEntityTypeConfiguration<ExecutionLog>
{
    public void Configure(EntityTypeBuilder<ExecutionLog> builder)
    {
        builder.ToTable("execution_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Level)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(l => l.Message)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(l => l.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(l => l.Execution)
            .WithMany(e => e.Logs)
            .HasForeignKey(l => l.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.ExecutionId);
        builder.HasIndex(l => l.CreatedAtUtc);
    }
}
