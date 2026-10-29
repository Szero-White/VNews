using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VNews.Domain.Entities;

namespace VNews.Infrastructure.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Slug).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Body).IsRequired();
        builder.Property(a => a.CoverImageUrl).HasMaxLength(500);
        builder.Property(a => a.AuthorName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.AuthorUserId).HasMaxLength(450);
        builder.Property(a => a.ReviewedByUserId).HasMaxLength(450);
        builder.Property(a => a.ReviewNote).HasMaxLength(1000);

        builder.HasIndex(a => a.Slug).IsUnique();
        builder.HasIndex(a => new { a.Status, a.PublishedAt });
        builder.HasIndex(a => a.AuthorUserId);

        builder.HasOne(a => a.Category)
            .WithMany(c => c.Articles)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
