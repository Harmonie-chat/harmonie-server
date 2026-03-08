using Harmonie.API.Middleware;
using Harmonie.API.RealTime;
using Harmonie.Application;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.LogoutAll;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.DeleteChannel;
using Harmonie.Application.Features.Channels.DeleteMessage;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Channels.UpdateChannel;
using Harmonie.Application.Features.Conversations.DeleteDirectMessage;
using Harmonie.Application.Features.Conversations.EditDirectMessage;
using Harmonie.Application.Features.Conversations.GetDirectMessages;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using Harmonie.Application.Features.Conversations.SendDirectMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.LeaveGuild;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Guilds.RemoveMember;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.SearchUsers;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Application.Features.Voice.HandleLiveKitWebhook;
using Harmonie.Application.Interfaces;
using Harmonie.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add layers
builder.Services.AddApplication();
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Uploads"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<ITextChannelNotifier, SignalRTextChannelNotifier>();
builder.Services.AddScoped<IVoicePresenceNotifier, SignalRVoicePresenceNotifier>();
builder.Services.AddScoped<IDirectMessageNotifier, SignalRDirectMessageNotifier>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("message-post", httpContext =>
    {
        var partitionKey = ResolveMessagePostPartitionKey(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

// Configure Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Harmonie API",
            Version = "v1",
            Description = "Open-source, self-hosted communication platform API"
        };

        return Task.CompletedTask;
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("Configuration value 'Jwt:Secret' is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && path.StartsWithSegments("/hubs/realtime"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();

                return EndpointExtensions.WriteErrorAsync(
                    context.Response,
                    new ApplicationError(
                        ApplicationErrorCodes.Auth.InvalidCredentials,
                        "Authentication is required to access this resource."));
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<GlobalExceptionHandler>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var storageProvider = app.Configuration["ObjectStorage:Provider"] ?? "s3";
if (string.Equals(storageProvider, "local", StringComparison.OrdinalIgnoreCase))
{
    var localBasePath = app.Configuration["ObjectStorage:LocalBasePath"] ?? "uploads";
    if (!Path.IsPathRooted(localBasePath))
        localBasePath = Path.GetFullPath(localBasePath);
    Directory.CreateDirectory(localBasePath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(localBasePath),
        RequestPath = "/files"
    });
}

// ============================================================
// MAP ENDPOINTS - Vertical Slice Architecture
// ============================================================

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck")
.WithTags("System")
.Produces(StatusCodes.Status200OK);

// Auth endpoints
RegisterEndpoint.Map(app);
LoginEndpoint.Map(app);
LogoutEndpoint.Map(app);
LogoutAllEndpoint.Map(app);
RefreshTokenEndpoint.Map(app);
CreateChannelEndpoint.Map(app);
CreateGuildEndpoint.Map(app);
ListUserGuildsEndpoint.Map(app);
InviteMemberEndpoint.Map(app);
GetGuildChannelsEndpoint.Map(app);
GetGuildVoiceParticipantsEndpoint.Map(app);
GetGuildMembersEndpoint.Map(app);
SearchMessagesEndpoint.Map(app);
LeaveGuildEndpoint.Map(app);
RemoveMemberEndpoint.Map(app);
UpdateMemberRoleEndpoint.Map(app);
TransferOwnershipEndpoint.Map(app);
SendMessageEndpoint.Map(app);
GetMessagesEndpoint.Map(app);
JoinVoiceChannelEndpoint.Map(app);
HandleLiveKitWebhookEndpoint.Map(app);
UpdateChannelEndpoint.Map(app);
DeleteChannelEndpoint.Map(app);
EditMessageEndpoint.Map(app);
DeleteMessageEndpoint.Map(app);
GetMyProfileEndpoint.Map(app);
SearchUsersEndpoint.Map(app);
UpdateMyProfileEndpoint.Map(app);
UploadFileEndpoint.Map(app);
OpenConversationEndpoint.Map(app);
ListConversationsEndpoint.Map(app);
GetDirectMessagesEndpoint.Map(app);
SearchConversationMessagesEndpoint.Map(app);
EditDirectMessageEndpoint.Map(app);
DeleteDirectMessageEndpoint.Map(app);
SendDirectMessageEndpoint.Map(app);
app.MapHub<RealtimeHub>("/hubs/realtime");

// Future endpoints will be added here as features are developed
// Example:
// GuildEndpoints.Map(app);
// ChannelEndpoints.Map(app);
// MessageEndpoints.Map(app);

// ============================================================

app.Run();

static string ResolveMessagePostPartitionKey(HttpContext httpContext)
{
    if (httpContext.TryGetAuthenticatedUserId(out var userId) && userId is not null)
        return $"user:{userId}";

    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return $"ip:{remoteIp}";
}

// Make Program class accessible to integration tests
public partial class Program { }
