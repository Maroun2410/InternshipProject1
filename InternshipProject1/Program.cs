using System.Globalization;
using InternshipProject1;
using InternshipProject1.Data;
using InternshipProject1.Filters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OrchardApp.Data.Seed;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

// ------------------------------------------------------------
// 1) Bootstrap minimal configuration BEFORE building the app,
//    so we can configure Serilog early (and log app start).
// ------------------------------------------------------------
var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var rawConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Default safe per-user directory (works on any machine)
var defaultLogDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Internship_In-Mobiles", "logs"
);

// Read from config if present; otherwise use fallback
var logDirectory = rawConfig["LoggingDirectory"] ?? defaultLogDir;
Directory.CreateDirectory(logDirectory);

// ------------------------------------------------------------
// 2) Configure Serilog (JSON lines to file + console).
//    Noise tuned down for Microsoft/*.
// ------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Internship_In-Mobiles")
    .WriteTo.File(
        new JsonFormatter(),
        path: Path.Combine(logDirectory, "app-.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14
    )
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

// ------------------------------------------------------------
// 3) Build the application
// ------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ---------------------- Localization ---------------------- //
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("fr"),
    new CultureInfo("ar")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider()); // ?culture=fr
});

// ---------------------- Services ---------------------- //
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddControllers(opts =>
    {
        // Response wrapper filter
        opts.Filters.Add<ApiResponseWrapperFilter>();
    })
    .AddDataAnnotationsLocalization()
    .AddViewLocalization();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Internship API", Version = "v1" });
});

// Health Checks
builder.Services.AddHealthChecks();

// Custom validation response (localized)
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

        var errors = context.ModelState
            .Where(e => e.Value.Errors.Count > 0)
            .Select(e => new
            {
                Field = e.Key,
                Errors = e.Value.Errors.Select(x => x.ErrorMessage)
            });

        var result = new
        {
            StatusCode = 400,
            Message = localizer["ValidationFailed"],
            Errors = errors
        };

        return new BadRequestObjectResult(result);
    };
});

// ---------------------- App pipeline ---------------------- //
var app = builder.Build();

try
{
    // 1) Localization early
    var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(locOptions.Value);

    // 2) Unified pipeline (Correlation + Error handling + Outcome logging + Wrapping)
    app.UseMiddleware<InternshipProject1.Middleware.UnifiedPipelineMiddleware>();

    // 3) Request logging (after unified pipeline so it picks up CorrelationId)
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("CorrelationId", InternshipProject1.Middleware.UnifiedPipelineMiddleware.GetCorrelationId(ctx));
            diag.Set("RequestPath", ctx.Request.Path);
            diag.Set("RequestMethod", ctx.Request.Method);
            diag.Set("UserAgent", ctx.Request.Headers["User-Agent"].ToString());
        };
    });

    // 4) Swagger (unified middleware already bypasses swagger paths)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Internship API v1");
        c.RoutePrefix = string.Empty;
    });

    // 5) Usual web plumbing
    app.UseHttpsRedirection();

    // If you have auth, keep this order:
    // app.UseAuthentication();
    app.UseAuthorization();

    // 6) Endpoints
    app.MapControllers();
    app.MapHealthChecks("/health");

    // 7) DB migration + seed
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
        DbInitializer.Seed(dbContext);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
