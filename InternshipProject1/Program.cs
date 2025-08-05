using InternshipProject1.Data;
using Microsoft.EntityFrameworkCore;
using OrchardApp.Data.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------- Services ---------------------- //
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

// Swagger with Basic Metadata
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Internship API", Version = "v1" });
});

// Add Health Checks
builder.Services.AddHealthChecks();

// ---------------------- Build App ---------------------- //
var app = builder.Build();

try
{
    // Global Exception Handling
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"An unexpected error occurred.\"}");
        });
    });

    // Enable Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Internship API v1");
        c.RoutePrefix = string.Empty;  // Swagger at root URL
    });

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // Health Check Endpoint
    app.MapHealthChecks("/health");

    // Apply Migrations & Seed Data
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
        DbInitializer.Seed(dbContext);
    }
    app.UseDeveloperExceptionPage();
    app.Run();
    //something
}
catch (Exception ex)
{
    Console.WriteLine($"Application failed to start: {ex.Message}");
}
