using DWSIM.OTS.Orchestrator.Models;
using DWSIM.OTS.Orchestrator.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/orchestrator-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DWSIM OTS Orchestrator API",
        Version = "v1",
        Description = "Session orchestration and management for DWSIM Operator Training System",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "DWSIM OTS",
            Url = new Uri("https://github.com/DanWBR/dwsim6")
        }
    });

    // Enable XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure database
var dbConfig = new DatabaseConfig
{
    ConnectionString = builder.Configuration.GetConnectionString("TimescaleDb")
        ?? "Host=localhost;Port=5432;Database=dwsim_ots;Username=postgres;Password=postgres",
    BatchSize = builder.Configuration.GetValue<int>("Database:BatchSize", 100),
    FlushIntervalSeconds = builder.Configuration.GetValue<int>("Database:FlushIntervalSeconds", 5)
};

// Configure pool
var poolConfig = new PoolConfig
{
    HostUrls = builder.Configuration.GetSection("Pool:HostUrls").Get<List<string>>()
        ?? new List<string> { "http://localhost:5000" },
    MaxSessionsPerHost = builder.Configuration.GetValue<int>("Pool:MaxSessionsPerHost", 10),
    HealthCheckIntervalSeconds = builder.Configuration.GetValue<int>("Pool:HealthCheckIntervalSeconds", 30),
    SessionTimeoutMinutes = builder.Configuration.GetValue<int>("Pool:SessionTimeoutMinutes", 240)
};

// Register services
builder.Services.AddSingleton(dbConfig);
builder.Services.AddSingleton(poolConfig);
builder.Services.AddSingleton<ITimescaleDbLogger, TimescaleDbLogger>();
builder.Services.AddSingleton<ISessionPoolManager, SessionPoolManager>();
builder.Services.AddSingleton<IAssessmentEngine, AssessmentEngine>();
builder.Services.AddHttpClient();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<OrchestratorHealthCheck>("orchestrator");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DWSIM OTS Orchestrator API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Log startup
Log.Information("DWSIM OTS Orchestrator starting...");
Log.Information("Database: {ConnectionString}", dbConfig.ConnectionString.Split(';')[0]);
Log.Information("Pool hosts: {HostCount}", poolConfig.HostUrls.Count);

app.Run();

// Cleanup
Log.CloseAndFlush();

/// <summary>
/// Health check for orchestrator service
/// </summary>
public class OrchestratorHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly ISessionPoolManager _poolManager;

    public OrchestratorHealthCheck(ISessionPoolManager poolManager)
    {
        _poolManager = poolManager;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _poolManager.GetHealthAsync();

            if (health.Status == "healthy")
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    "Orchestrator is healthy",
                    new Dictionary<string, object>
                    {
                        ["active_sessions"] = health.Statistics?.ActiveSessions ?? 0,
                        ["available_hosts"] = health.Statistics?.AvailableHosts ?? 0
                    });
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                "Orchestrator is degraded",
                null,
                new Dictionary<string, object>
                {
                    ["status"] = health.Status
                });
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Orchestrator is unhealthy",
                ex);
        }
    }
}
