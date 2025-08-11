using InternshipProject1.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrchardApp.Data.Seed;
using InternshipProject1.Filters;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using InternshipProject1;

var builder = WebApplication.CreateBuilder(args);

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
    options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider()); // allows ?culture=fr
});

// ---------------------- Services ---------------------- //
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers(options =>
{
    options.Filters.Add<InternshipProject1.Filters.ApiResponseWrapperFilter>();
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
            Message = localizer["ValidationFailed"], // must exist in your .resx files
            Errors = errors
        };

        return new BadRequestObjectResult(result);
    };
});

// ---------------------- Build App ---------------------- //
var app = builder.Build();

try
{
    // Localization middleware
    var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(locOptions.Value);

    // Error handler (uses localization inside)
    app.UseMiddleware<InternshipProject1.Middleware.ErrorHandlingMiddleware>();

    // Swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Internship API v1");
        c.RoutePrefix = string.Empty;
    });

    app.UseHttpsRedirection();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // DB migration + seed
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
    Console.WriteLine($"Application failed to start: {ex.Message}");
}
