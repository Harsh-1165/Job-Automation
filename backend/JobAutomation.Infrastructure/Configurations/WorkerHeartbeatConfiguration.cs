using JobAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobAutomation.Infrastructure.Configurations;

public class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        builder.ToTable("worker_heartbeats");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.WorkerId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(w => w.HostName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(w => w.StartedAtUtc)
            .IsRequired();

        builder.Property(w => w.LastHeartbeatAtUtc)
            .IsRequired();

        builder.HasIndex(w => w.WorkerId)
            .IsUnique();

        builder.HasIndex(w => w.LastHeartbeatAtUtc);
    }
}
