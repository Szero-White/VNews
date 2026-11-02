using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VNews.Domain.Entities;

namespace VNews.Infrastructure.Persistence.Configurations;

public sealed class ArticleImageConfiguration : IEntityTypeConfiguration<ArticleImage>
{
    public void Configure(EntityTypeBuilder<ArticleImage> builder)
    {
        builder.ToTable("ArticleImages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.ArticleId, x.SortOrder });
        builder.HasOne(x => x.Article).WithMany(a => a.Images).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
    }
}
