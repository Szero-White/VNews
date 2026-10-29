using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VNews.Domain.Entities;

namespace VNews.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.UserDisplayName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Detail).HasMaxLength(1000);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
