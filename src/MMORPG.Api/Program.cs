using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using MMORPG.Api.Configuration;
using MMORPG.Api.Data;
using MMORPG.Api.Middleware;
using MMORPG.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero
        };

        // Allow JWT from SignalR query string; check JTI blacklist on every validated token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (jti is null) return;
                var cacheService = ctx.HttpContext.RequestServices.GetRequiredService<ICacheService>();
                if (await cacheService.ExistsAsync(CacheKeys.JtiRevoked(jti)))
                    ctx.Fail("Token has been revoked.");
            }
        };
    });

builder.Services.AddAuthorization();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("global", limiter =>
    {
        limiter.PermitLimit        = builder.Configuration.GetValue<int>("RateLimit:PermitLimit");
        limiter.Window             = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimit:WindowSeconds"));
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit         = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Application services
builder.Services.Configure<NameValidationOptions>(
    builder.Configuration.GetSection(NameValidationOptions.SectionName));
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

// SignalR
builder.Services.AddSignalR();

// Redis (distributed cache — AddStackExchangeRedisCache requires Microsoft.Extensions.Caching.StackExchangeRedis)
builder.Services.AddDistributedMemoryCache(); // swap for Redis in production

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Ethereal Dreams MMORPG API",
        Version = "v1",
        Description = "REST API for the Ethereal Dreams MMORPG backend."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header
    });

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

builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("global");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Expose Program to WebApplicationFactory in the test project
public partial class Program { }
