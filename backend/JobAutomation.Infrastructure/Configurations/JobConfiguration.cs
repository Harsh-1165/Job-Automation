using JobAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobAutomation.Infrastructure.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Description)
            .HasMaxLength(2000);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(j => j.HttpMethod)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(j => j.TargetUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(j => j.CronExpression)
            .HasMaxLength(100);

        builder.Property(j => j.CreatedAtUtc)
            .IsRequired();

        builder.Property(j => j.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(j => j.User)
            .WithMany(u => u.Jobs)
            .HasForeignKey(j => j.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => j.UserId);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.NextRunAtUtc);
    }
}
