using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StockPulse.API.Middleware;
using StockPulse.Application;
using StockPulse.Infrastructure;
using StockPulse.Infrastructure.Persistence;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationIdHeader());

// Controllers + JSON enums as strings
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StockPulse — Intelligent Inventory Dashboard",
        Version = "v1",
        Description = "CBTW Technical Assessment — Scenario B. Auth is out of scope; all endpoints are public."
    });
    c.UseInlineDefinitionsForEnums();
});

// Application + Infrastructure DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<StockPulse.Application.Common.InventorySettings>(options =>
{
    options.DealershipId = Guid.Parse(builder.Configuration["DealershipId"]!);
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "database",
        tags: ["db", "sql"]);

// OpenTelemetry Metrics + Tracing
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]!;
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("StockPulse"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("StockPulse")
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));

var app = builder.Build();

// Apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.Migrate();
}

// Configure Hangfire recurring jobs
var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
StockPulse.Infrastructure.DependencyInjection.ConfigureHangfireJobs(builder.Configuration, recurringJobManager);

// Middleware pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "StockPulse v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "StockPulse Inventory API";
});

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            components = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapPrometheusScrapingEndpoint("/metrics");

app.UseHangfireDashboard("/hangfire");

app.Run();

public partial class Program { }
