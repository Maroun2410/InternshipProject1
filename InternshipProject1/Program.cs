using InternshipProject1.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrchardApp.Data.Seed;
using InternshipProject1.Filters;


var builder = WebApplication.CreateBuilder(args);

// ---------------------- Services ---------------------- //
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers(options =>
{
    options.Filters.Add<InternshipProject1.Filters.ApiResponseWrapperFilter>();
});


// Swagger with Basic Metadata
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Internship API", Version = "v1" });
});

// Add Health Checks
builder.Services.AddHealthChecks();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
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
            Message = "Validation failed.",
            Errors = errors
        };

        return new BadRequestObjectResult(result);
    };
});

// ---------------------- Build App ---------------------- //
var app = builder.Build();

try
{
    // Custom global error handler
    app.UseMiddleware<InternshipProject1.Middleware.ErrorHandlingMiddleware>();

    // Enable Swagger
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

    // Migrate & Seed Database
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
        DbInitializer.Seed(dbContext);
    }

    app.Run();
    //something
}
catch (Exception ex)
{
    Console.WriteLine($"Application failed to start: {ex.Message}");
}

