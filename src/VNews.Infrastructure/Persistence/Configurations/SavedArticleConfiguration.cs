using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VNews.Domain.Entities;
using VNews.Domain.Identity;

namespace VNews.Infrastructure.Persistence.Configurations;

public sealed class SavedArticleConfiguration : IEntityTypeConfiguration<SavedArticle>
{
    public void Configure(EntityTypeBuilder<SavedArticle> builder)
    {
        builder.ToTable("SavedArticles");
        builder.HasKey(x => new { x.UserId, x.ArticleId });
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => x.ArticleId);
        builder.Property(x => x.SavedAtUtc).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Article).WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
    }
}
