using Alt_Support.Configuration;
using Alt_Support.Data;
using Alt_Support.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Configure application settings
builder.Services.Configure<ApplicationConfiguration>(
    builder.Configuration.GetSection("ApplicationConfiguration"));

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                          builder.Configuration["ApplicationConfiguration:DatabaseConnectionString"] ??
                          "Data Source=tickets.db";
    options.UseSqlite(connectionString);
});

// Add HTTP client for Jira API
builder.Services.AddHttpClient<IJiraService, JiraService>();

// Register application services
builder.Services.AddScoped<IJiraService, JiraService>();
builder.Services.AddScoped<ISimilarityService, SimilarityService>();
builder.Services.AddScoped<ITicketDataService, TicketDataService>();
builder.Services.AddScoped<ITicketAnalysisService, TicketAnalysisService>();

// Add background service for historical data sync
builder.Services.AddHostedService<HistoricalDataSyncService>();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Alt Support API",
        Version = "v1",
        Description = "Production Support Ticket Analysis System"
    });
});

// Add CORS for frontend integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Alt Support API V1");
        c.RoutePrefix = "swagger"; // Move Swagger to /swagger
    });
}

// Enable static files BEFORE other middleware
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Add a simple status endpoint
app.MapGet("/status", () => new
{
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0"
});

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize database");
    }
}

app.Logger.LogInformation("Alt Support API is starting...");

app.Run();
