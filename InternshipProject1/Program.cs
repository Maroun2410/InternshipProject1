using InternshipProject1;
using InternshipProject1.Data;
using InternshipProject1.Filters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OrchardApp.Data.Seed;
using Serilog;
using Serilog.Formatting.Json;
using System.Globalization;

//
// ---------------------- Logging (Serilog) ---------------------- //
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Internship_In-Mobiles")
    .WriteTo.File(new JsonFormatter(),
        path: "logs/app-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .WriteTo.Console(new JsonFormatter())
    .MinimumLevel.Information()
    .CreateLogger();

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

    // Allow switching via ?culture=fr
    options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
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

// ---------------------- Build App ---------------------- //
var app = builder.Build();

try
{
    // 1) Localization early
    var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(locOptions.Value);

    // 2) Correlation FIRST (so everything downstream has it)
    app.UseMiddleware<InternshipProject1.Middleware.CorrelationIdMiddleware>();

    // 3) Request logging (after correlation so it picks up CorrelationId)
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("CorrelationId", InternshipProject1.Middleware.CorrelationIdMiddleware.Get(ctx));
            diag.Set("RequestPath", ctx.Request.Path);
            diag.Set("RequestMethod", ctx.Request.Method);
            diag.Set("UserAgent", ctx.Request.Headers["User-Agent"].ToString());
        };
    });

    // 4) Global error handler (catches everything after this point)
    app.UseMiddleware<InternshipProject1.Middleware.ErrorHandlingMiddleware>();

    // 5) Swagger (your error middleware already bypasses swagger paths)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Internship API v1");
        c.RoutePrefix = string.Empty;
    });

    // 6) Usual web plumbing
    app.UseHttpsRedirection();

    // If you have auth, keep this order:
    // app.UseAuthentication();
    app.UseAuthorization();

    // 7) Endpoints
    app.MapControllers();
    app.MapHealthChecks("/health");

    // 8) DB migration + seed
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

