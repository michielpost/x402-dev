using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using ProtoBuf.Grpc.Server;
using Swashbuckle.AspNetCore.Filters;
using System.Reflection;
using System.Threading.RateLimiting;
using x402;
using x402.Coinbase;
using x402.Coinbase.Models;
using x402dev.Database;
using x402dev.Server.HostedServices;
using x402dev.Server.Services;
using x402dev.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddFluentUIComponents();

// ---- Register the two policies (they are NOT applied globally) ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Max 4 requests every 10 seconds (per IP)
    options.AddPolicy("proxy-3sec", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 4,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                TokensPerPeriod = 4,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Max 100 requests in 24 h (per IP)
    options.AddPolicy("proxy-24h", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromHours(24),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var corsPolicyName = "AllowCredentialsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            var host = uri.Host;
            return host == "x402-dev.pages.com"
            || host.EndsWith("x402-dev.pages.com")
            || host.EndsWith("x402dev.com")
            || host == "x402dev.com"
            || host.Contains("localhost"); // dev only
        })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddGrpc();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});
builder.Services.AddCodeFirstGrpc();

builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "x402dev API", Version = "v1" });
    c.ExampleFilters();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});
builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ContentService>();
builder.Services.AddScoped<PublicMessagesService>();

builder.Services.AddScoped<PublicMessagesService>();


builder.Services.Configure<CoinbaseOptions>(builder.Configuration.GetSection(nameof(CoinbaseOptions)));

var facilitatorUrl = builder.Configuration["FacilitatorUrl"];
if (!string.IsNullOrEmpty(facilitatorUrl))
{
    builder.Services.AddX402().WithHttpFacilitator(facilitatorUrl);

}
else
{
    builder.Services.AddX402().WithCoinbaseFacilitator(builder.Configuration);
}


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
    //db.Database.EnsureCreated();
    db.Database.Migrate(); // Creates the database if it does not exist and applies any pending migrations
}

var contentService = app.Services.GetRequiredService<ContentService>();
await contentService.Initialize();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseCors(corsPolicyName);

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

// Rate limiting middleware must be enabled for per-endpoint attributes to work
// Must be after UseRouting() for attribute-based rate limiting to work correctly
app.UseRateLimiter();

app.UseGrpcWeb();
app.MapGrpcService<FacilitatorGrpcService>().EnableGrpcWeb();
app.MapGrpcService<ContentGrpcService>().EnableGrpcWeb();
app.MapGrpcService<PublicMessageGrpcService>().EnableGrpcWeb();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "x402dev API");
});



app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
