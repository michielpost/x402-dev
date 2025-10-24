using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using x402;
using x402.Coinbase.Models;
using x402dev.Web;
using x402dev.Web.Data;
using x402dev.Web.HostedServices;
using x402dev.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var facilitatorUrl = builder.Configuration["FacilitatorUrl"];
if (!string.IsNullOrEmpty(facilitatorUrl))
{
    builder.Services.AddX402().WithHttpFacilitator(facilitatorUrl);

}

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "x402dev API", Version = "v1" });
});


builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ContentService>();
builder.Services.AddScoped<PublicMessagesService>();

builder.Services.Configure<CoinbaseOptions>(builder.Configuration.GetSection(nameof(CoinbaseOptions)));

//Background Hosted Services
builder.Services.AddHostedService<ContentSyncBackgroundService>();
builder.Services.AddHostedService<FacilitatorTestBackgroundService>();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
});

var app = builder.Build();

var sqlLiteBuilder = new SqliteConnectionStringBuilder(connectionString);
var dbPath = Path.GetDirectoryName(sqlLiteBuilder.DataSource);
if (dbPath != null && !Directory.Exists(dbPath))
{
    Directory.CreateDirectory(dbPath);
}

// Ensure database is created and apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // Creates the database if it does not exist and applies any pending migrations
}

var contentService = app.Services.GetRequiredService<ContentService>();
await contentService.Initialize();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseMiddleware<SecurityHeaderMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "x402dev API");
});

app.Run();
