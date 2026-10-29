using Microsoft.EntityFrameworkCore;
using VNews.Domain.Entities;

namespace VNews.Infrastructure.Persistence;

public class VNewsDbContext : DbContext
{
    public VNewsDbContext(DbContextOptions<VNewsDbContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VNewsDbContext).Assembly);
    }
}
