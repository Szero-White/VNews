using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence.ProcedureResults;

namespace VNews.Infrastructure.Persistence;

public class VNewsDbContext : IdentityDbContext<ApplicationUser>
{
    public VNewsDbContext(DbContextOptions<VNewsDbContext> options) : base(options)
    {
    }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ArticleByCategoryResult> ArticleByCategoryResults => Set<ArticleByCategoryResult>();
    public DbSet<DailyPublishStatResult> DailyPublishStatResults => Set<DailyPublishStatResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VNewsDbContext).Assembly);

        modelBuilder.Entity<ArticleByCategoryResult>(b =>
        {
            b.HasNoKey();
            b.ToView(null);
        });

        modelBuilder.Entity<DailyPublishStatResult>(b =>
        {
            b.HasNoKey();
            b.ToView(null);
        });
    }
}
