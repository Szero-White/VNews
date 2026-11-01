using VNews.Web.Startup;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddVNewsWeb(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.InitializeAsync();
app.UseVNewsPipeline();
app.MapVNewsRoutes();
app.Run();

public partial class Program;
