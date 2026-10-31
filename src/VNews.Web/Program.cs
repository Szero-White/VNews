using Microsoft.AspNetCore.Identity;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Infrastructure;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Seed;
using VNews.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
    await DbInitializer.SeedAsync(context);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await IdentitySeeder.SeedAsync(roleManager, userManager, seedDemoAccounts: app.Environment.IsDevelopment());
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/PageNotFound", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "article",
    pattern: "tin/{slug}",
    defaults: new { controller = "Article", action = "Detail" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "category",
    pattern: "chuyen-muc/{slug}",
    defaults: new { controller = "Category", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "tag",
    pattern: "chu-de/{slug}",
    defaults: new { controller = "Tag", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "search",
    pattern: "tim-kiem",
    defaults: new { controller = "Search", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-login",
    pattern: "tai-khoan/dang-nhap",
    defaults: new { controller = "Account", action = "Login" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-register",
    pattern: "tai-khoan/dang-ky",
    defaults: new { controller = "Account", action = "Register" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-profile",
    pattern: "tai-khoan/ho-so",
    defaults: new { controller = "Account", action = "Profile" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "cms-login",
    pattern: "cms/dang-nhap",
    defaults: new { controller = "Account", action = "CmsLogin" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-logout",
    pattern: "tai-khoan/dang-xuat",
    defaults: new { controller = "Account", action = "Logout" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-denied",
    pattern: "tai-khoan/khong-co-quyen",
    defaults: new { controller = "Account", action = "AccessDenied" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "cms",
    pattern: "cms/{controller=CmsArticles}/{action=Index}/{id?}",
    constraints: new { controller = "CmsArticles|CmsCategories|CmsUsers" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
