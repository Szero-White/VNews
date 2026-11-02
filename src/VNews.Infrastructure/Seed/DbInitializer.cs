using Microsoft.EntityFrameworkCore;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Seed;

public static class DbInitializer
{
    /// <summary>
    /// Brings the schema up to date and, only when <paramref name="seedSampleContent"/> is true, fills an EMPTY database with the sample categories
    /// and articles. A real deployment passes false: it must show the content of the database it was given, never invent any.
    /// </summary>
    public static async Task SeedAsync(VNewsDbContext context, bool seedSampleContent = true)
    {
        await context.Database.MigrateAsync();
        if (!seedSampleContent) return;

        if (!await context.Categories.AnyAsync())
        {
            var categories = CategorySeedData.Categories
                .Select(c => new Category
                {
                    Name = c.Name,
                    Slug = c.Slug,
                    Description = c.Description,
                    DisplayOrder = c.Order
                })
                .ToList();

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        if (!await context.Articles.AnyAsync())
        {
            var categories = await context.Categories.ToListAsync();
            var articles = ArticleSeedData.Build(categories);

            context.Articles.AddRange(articles);
            await context.SaveChangesAsync();
        }
    }
}
