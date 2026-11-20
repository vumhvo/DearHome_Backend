using DearHome_Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DearHome_Backend.Helpers;
using DearHome_Backend.Models;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;
using DearHome_Backend.Middlewares;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.Text.Json;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DearHome_Backend.Config;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
    options.JsonSerializerOptions.AllowTrailingCommas = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContextFactory<DearHomeContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

    // SQL/parameter logging only in Development: it can expose user data.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging()
               .LogTo(Console.WriteLine, LogLevel.Information);
    }
},
    ServiceLifetime.Scoped);

builder.Services.AddIdentity<User, IdentityRole<Guid>>() // Use IdentityRole<Guid>
    .AddEntityFrameworkStores<DearHomeContext>()
    .AddDefaultTokenProviders();

ServiceExtensions.AddScopedServices(builder.Services);
ServiceExtensions.AddScopedRepositories(builder.Services);
ServiceExtensions.AddHttpClients(builder.Services, builder.Configuration);
builder.Services.Configure<NatsConfig>(builder.Configuration.GetSection("Nats"));

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

//Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT key is not configured.");
}
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// Enable dependency tracking for HttpClient
builder.Services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) => {
    module.EnableSqlCommandTextInstrumentation = true;
});

builder.Services.AddAutoMapper(typeof(MappingProfile));

// CORS: allowed origins come from Cors:AllowedOrigins. If none are configured, fall back to
// allowing any origin so existing deployments keep working (a warning is logged at startup).
const string CorsPolicyName = "DefaultCorsPolicy";
var corsAllowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsAllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsAllowedOrigins);
        }
        else
        {
            policy.AllowAnyOrigin();
        }

        policy.AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var firebaseCredentialPath = builder.Configuration["Firebase:CredentialPath"];
if (string.IsNullOrWhiteSpace(firebaseCredentialPath))
{
    firebaseCredentialPath = "Credentials/makemyhome-27df4-firebase-adminsdk-hsal7-09e697d0ff.json";
}

if (FirebaseApp.DefaultInstance == null)
{
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromFile(firebaseCredentialPath)
    });
}

// Development environment: Use in-memory cache
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDistributedMemoryCache();
}
// Production environment: Use Azure Redis Cache
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration["Redis:ConnectionString"];
        options.InstanceName = builder.Configuration["Redis:InstanceName"];
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = string.Empty;
    });
}

if (corsAllowedOrigins.Length == 0)
{
    app.Logger.LogWarning("Cors:AllowedOrigins is not configured; CORS allows any origin. Configure the allowed frontend origins for production.");
}

try
{
    using (var connection = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")))
    {
        connection.Open();
        app.Logger.LogInformation("Database connection successful.");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database connection failed: {Message}", ex.Message);
}

app.UseMiddleware<ErrorHandlerMiddleware>();

app.UseCors(CorsPolicyName);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
