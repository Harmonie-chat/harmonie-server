using System.Text.Json.Serialization;
using Harmonie.API.Configuration;
using Harmonie.API.Endpoints;
using Harmonie.API.Middleware;
using Harmonie.API.RealTime.Common;
using Harmonie.Application;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Uploads"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

builder.Services.AddRealTime();
builder.Services.AddApiHealthChecks();
builder.Services.AddApiRateLimiter();
builder.Services.AddApiDocumentation();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApiCors(builder.Configuration, builder.Environment);

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<GlobalExceptionHandler>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("ApiCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthenticatedUserContextMiddleware>();
app.UseRateLimiter();

// Endpoints
app.MapApiHealthChecks();
app.MapAuthEndpoints();
app.MapGuildEndpoints();
app.MapChannelEndpoints();
app.MapConversationEndpoints();
app.MapUserEndpoints();
app.MapUploadEndpoints();
app.MapVoiceEndpoints();
app.MapHub<RealtimeHub>("/hubs/realtime");

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
