using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Interfaces;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Services;

namespace VNews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' chưa được cấu hình.");

        services.AddDbContext<VNewsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(VNewsDbContext).Assembly.FullName)));

        services.AddScoped<IArticleQueryService, ArticleQueryService>();
        services.AddScoped<ICategoryQueryService, CategoryQueryService>();
        services.AddScoped<IArticleStoredProcedureService, ArticleStoredProcedureService>();

        return services;
    }
}
