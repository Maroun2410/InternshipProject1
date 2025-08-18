using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MobileAPI.Auth;
using MobileAPI.Infrastructure;
using MobileAPI.Services;
// alias our email interfaces/impls to avoid clashes with Identity UI
using IAppEmailSender = MobileAPI.Email.IEmailSender;
using DevEmailSender = MobileAPI.Email.DevEmailSender;
using SesEmailSender = MobileAPI.Email.SesEmailSender;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Config
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// ---------------- DbContext (PostgreSQL)
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));

// ---------------- HttpContext + CurrentUser accessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserFromHttpContext>();

// ---------------- Identity (GUID keys)
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.SignIn.RequireConfirmedEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Claims principal factory for SignInManager deps
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>,
    UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>>();

builder.Services.Configure<DataProtectionTokenProviderOptions>(o =>
{
    o.TokenLifespan = TimeSpan.FromHours(2); // email confirm/reset tokens
});

// ---------------- AuthN: JWT (HS256 for dev)
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };

        // TEMP: log failures so 401s are easy to diagnose
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine("JWT auth failed: " + ctx.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                Console.WriteLine("JWT challenge: " + (ctx.ErrorDescription ?? "(no desc)"));
                return Task.CompletedTask;
            }
        };
    });

// ---------------- AuthZ
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnlyWrite", p => p.RequireRole("Owner"));
    options.AddPolicy("OwnerOrWorkerRead", p => p.RequireRole("Owner", "Worker"));
});

// ---------------- App services
var emailProvider = builder.Configuration["Email:Provider"] ?? "Dev";
if (emailProvider.Equals("SES", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IAppEmailSender, SesEmailSender>();
else
    builder.Services.AddSingleton<IAppEmailSender, DevEmailSender>();

builder.Services.AddHostedService<RoleSeederHostedService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// ---------------- MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(builder.Configuration["Swagger:Version"] ?? "v1",
        new OpenApiInfo { Title = builder.Configuration["Swagger:Title"] ?? "MobileAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the JWT ONLY (no 'Bearer ' prefix).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Reference the scheme by ID so Swagger actually sends the header
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<UnifiedPipelineMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));

app.Run();
