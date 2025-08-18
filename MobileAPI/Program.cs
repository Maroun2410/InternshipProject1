using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileAPI.Auth;
using MobileAPI.Email;
using MobileAPI.Infrastructure;
using MobileAPI.Services;
using System.Text;
using DevEmailSender = MobileAPI.Email.DevEmailSender;
using IAppEmailSender = MobileAPI.Email.IEmailSender;
using SesEmailSender = MobileAPI.Email.SesEmailSender;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Config
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// ---------------- DbContext (PostgreSQL)
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));

// ✅ SignInManager needs HttpContextAccessor
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUser, CurrentUserFromHttpContext>();

// ---------------- Identity (GUID keys)  ✅ ensure AddSignInManager and ClaimsPrincipalFactory
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
    .AddSignInManager() // ✅ THIS registers SignInManager<ApplicationUser>
    .AddDefaultTokenProviders();

// Some hosts need this explicit registration for SignInManager deps:
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

// ---------------- MVC + Swagger (unchanged)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(builder.Configuration["Swagger:Version"] ?? "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        { Title = builder.Configuration["Swagger:Title"] ?? "MobileAPI", Version = "v1" });

    var scheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { [scheme] = new List<string>() });
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
