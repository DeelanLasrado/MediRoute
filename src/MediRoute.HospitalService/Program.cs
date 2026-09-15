using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using MediRoute.HospitalService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();

// Faster startup on Free F1 tier
builder.WebHost.ConfigureKestrel(o => o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2));

var sqlConn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1433;Database=MediRouteDb;User Id=sa;Password=MediRoute@Pass123;TrustServerCertificate=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<MediRouteDbContext>(options =>
    options.UseSqlServer(sqlConn, sql => sql.EnableRetryOnFailure(3)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<MediRouteDbContext>()
    .AddDefaultTokenProviders();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "MediRoute-Super-Secret-Key-Change-In-Production-Min32Chars!";
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MediRoute",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MediRoute",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn)
    && !redisConn.Contains("localhost", StringComparison.OrdinalIgnoreCase)
    && !redisConn.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConn));
}

var enableHangfire = !string.Equals(
    builder.Configuration["DisableHangfire"], "true", StringComparison.OrdinalIgnoreCase);
if (enableHangfire)
{
    try
    {
        builder.Services.AddHangfire(config =>
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(sqlConn, new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.FromSeconds(15)
                }));
        builder.Services.AddHangfireServer();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Hangfire setup deferred: {ex.Message}");
    }
}

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MediRoute Hospital Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<MediRouteDbContext>();
            await db.Database.EnsureCreatedAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "Admin", "HospitalAdmin", "Paramedic", "CityOfficial" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByEmailAsync("admin@mediroute.com") is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@mediroute.com",
                    Email = "admin@mediroute.com",
                    FullName = "System Admin",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(admin, "Admin@123");
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            logger.LogInformation("Database initialized");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DB init attempt {Attempt}/10 failed — retrying in 5s", attempt);
            if (attempt == 10)
            {
                // Don't crash the process on Free tier cold starts — listen and retry on requests
                logger.LogError(ex, "Database init failed after 10 attempts; starting without seed");
            }
            else
            {
                await Task.Delay(5000);
            }
        }
    }
}

// Swagger enabled in all environments for demo/portfolio hosting
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

if (enableHangfire && app.Services.GetService<IBackgroundJobClient>() is not null)
    app.UseHangfireDashboard("/hangfire");

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "HospitalService" }));
app.MapFallbackToFile("index.html");

app.Run();
