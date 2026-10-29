using Microsoft.EntityFrameworkCore;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(VNewsDbContext context)
    {
        await context.Database.MigrateAsync();

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
