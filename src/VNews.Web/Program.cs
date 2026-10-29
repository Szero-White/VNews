using VNews.Infrastructure;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
    await DbInitializer.SeedAsync(context);
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
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
