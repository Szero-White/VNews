using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VNews.Domain.Entities;

namespace VNews.Infrastructure.Persistence.Configurations;

public sealed class ArticleViewConfiguration : IEntityTypeConfiguration<ArticleView>
{
    public void Configure(EntityTypeBuilder<ArticleView> builder)
    {
        builder.ToTable("ArticleViews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VisitorHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ViewedAtUtc);
        builder.HasIndex(x => new { x.ArticleId, x.VisitorHash, x.ViewedAtUtc });
        builder.HasOne(x => x.Article).WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
    }
}
