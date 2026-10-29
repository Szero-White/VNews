using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
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

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<VNewsDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/tai-khoan/dang-nhap";
            options.AccessDeniedPath = "/tai-khoan/khong-co-quyen";
            options.LogoutPath = "/tai-khoan/dang-xuat";
        });

        services.AddScoped<IArticleQueryService, ArticleQueryService>();
        services.AddScoped<ICategoryQueryService, CategoryQueryService>();
        services.AddScoped<IArticleStoredProcedureService, ArticleStoredProcedureService>();
        services.AddScoped<IArticleCommandService, ArticleCommandService>();
        services.AddScoped<ICategoryCommandService, CategoryCommandService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
