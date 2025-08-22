using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MobileAPI.Auth;
using MobileAPI.Infrastructure;
using MobileAPI.Services;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

using DevEmailSender = MobileAPI.Email.DevEmailSender;
using IAppEmailSender = MobileAPI.Email.IEmailSender;
using SesEmailSender = MobileAPI.Email.SesEmailSender;

var builder = WebApplication.CreateBuilder(args);

// ---------- Controllers + success envelope
builder.Services.AddControllers(options =>
{
    options.Filters.Add<SuccessEnvelopeFilter>();
});

// ---------- Configuration objects
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// ---------- HttpContext + CurrentUser accessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserFromHttpContext>();

// ---------- RLS session interceptor
builder.Services.AddScoped<RlsSessionInterceptor>();

// ---------- DbContext (PostgreSQL) + interceptor
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;
    opt.UseNpgsql(cs);
    opt.AddInterceptors(sp.GetRequiredService<RlsSessionInterceptor>());
});

// ---------- Identity (GUID keys)
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.SignIn.RequireConfirmedEmail = true; // required for login
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Claims factory
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>,
    UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>>();

// Email confirmation token window
builder.Services.Configure<DataProtectionTokenProviderOptions>(o =>
{
    o.TokenLifespan = TimeSpan.FromHours(2);
});

// ---------- Data Protection (persist keys so tokens survive restarts)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "dpkeys")))
    .SetApplicationName("InternshipProject1MobileAPI");

// ---------- Forwarded headers (for tunnels/reverse proxies)
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ---------- AuthN: JWT (HS256 for dev)
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

// ---------- AuthZ
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnlyWrite", p => p.RequireRole("Owner"));
    options.AddPolicy("OwnerOrWorkerRead", p => p.RequireRole("Owner", "Worker"));
});

// ---------- CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileCors", p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// ---------- Email services
var emailProvider = builder.Configuration["Email:Provider"] ?? "Dev";
if (emailProvider.Equals("SES", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAppEmailSender, SesEmailSender>();
}
else if (emailProvider.Equals("SMTP", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<MobileAPI.Email.SmtpOptions>(builder.Configuration.GetSection("Smtp"));
    builder.Services.AddSingleton<IAppEmailSender, MobileAPI.Email.SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IAppEmailSender, DevEmailSender>();
}

// ---------- Hosted services (roles + demo data)
builder.Services.AddHostedService<RoleSeederHostedService>();
builder.Services.AddHostedService<DemoDataSeeder>(); // DEMO DATA ON EVERY RUN

builder.Services.AddScoped<ITokenService, TokenService>();

// ---------- Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(builder.Configuration["Swagger:Version"] ?? "v1",
        new OpenApiInfo
        {
            Title = builder.Configuration["Swagger:Title"] ?? "MobileAPI",
            Version = builder.Configuration["Swagger:Version"] ?? "v1",
            Description = "Farm Owner/Worker API with JWT auth, role-based access, and consistent envelopes."
        });

    c.TagActionsBy(api =>
        new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "API" });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the JWT ONLY (no 'Bearer ' prefix).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });

    c.OperationFilter<StandardResponsesOperationFilter>();
    c.SchemaFilter<ExampleSchemaFilter>();

    c.CustomOperationIds(api =>
    {
        var ctrl = api.ActionDescriptor.RouteValues["controller"];
        var method = api.HttpMethod;
        var name = api.TryGetMethodInfo(out var info) ? info.Name : "Op";
        return $"{ctrl}_{method}_{name}";
    });
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        var pd = new ValidationProblemDetails(errors)
        {
            Type = "about:blank",
            Title = "Validation failed.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        pd.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        var result = new BadRequestObjectResult(pd);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});

// ---------- Cleanup options + hosted service
builder.Services.Configure<CleanupOptions>(builder.Configuration.GetSection("Cleanup"));
builder.Services.AddHostedService<BackgroundCleanupHostedService>();

var app = builder.Build();

// Forwarded headers must come early
app.UseForwardedHeaders();

// Swagger (you can enable always if you prefer)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Your single middleware
app.UseMiddleware<UnifiedPipelineMiddleware>();

app.UseStatusCodePages(async statusCtx =>
{
    var http = statusCtx.HttpContext;
    var status = http.Response.StatusCode;

    if (status is 401 or 403 or 404)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = status switch
            {
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                _ => "Error"
            },
            Status = status,
            Instance = http.Request.Path
        };
        problem.Extensions["traceId"] = http.TraceIdentifier;

        http.Response.ContentType = "application/problem+json";
        await http.Response.WriteAsJsonAsync(problem);
    }
});

app.UseCors("MobileCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));

app.Run();
