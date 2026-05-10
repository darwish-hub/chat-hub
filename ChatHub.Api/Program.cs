using ChatHub.Api.Handlers;
using ChatHub.Api.HealthChecks;
using ChatHub.Api.Metrics;
using ChatHub.Api.Middleware;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Cache;
using ChatHub.Infrastructure.Nats;
using ChatHub.Infrastructure.Persistence;
using ChatHub.Infrastructure.Storage;
using ChatHub.Infrastructure.WebSockets;
using ChatHub.Infrastructure.Writers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter.Prometheus;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration.AddEnvironmentVariables();

// Configure settings
builder.Services.Configure<ChatHubSettings>(options =>
{
    options.MaxMessageSizeBytes = builder.Configuration.GetValue<int>("CHATHUB_MAX_MESSAGE_SIZE_BYTES", 65536);
    options.PingIntervalSeconds = builder.Configuration.GetValue<int>("CHATHUB_PING_INTERVAL_SECONDS", 15);
    options.IdleTimeoutMinutes = builder.Configuration.GetValue<int>("CHATHUB_IDLE_TIMEOUT_MINUTES", 30);
    options.RateLimitTextPerMinute = builder.Configuration.GetValue<int>("CHATHUB_RATE_LIMIT_TEXT_PER_MINUTE", 100);
    options.RateLimitVoicePerMinute = builder.Configuration.GetValue<int>("CHATHUB_RATE_LIMIT_VOICE_PER_MINUTE", 10);
    options.PodId = builder.Configuration.GetValue<string>("POD_ID", "unknown")!;
    
    var origins = builder.Configuration.GetValue<string>("CHATHUB_ALLOWED_ORIGINS", "");
    options.AllowedOrigins = origins?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
});

builder.Services.Configure<MongoSettings>(options =>
{
    options.ConnectionString = builder.Configuration.GetValue<string>("MONGO_CONNECTION_STRING", "mongodb://localhost:27017/chathub")!;
    options.DatabaseName = builder.Configuration.GetValue<string>("MONGO_DATABASE_NAME", "chathub")!;
});

builder.Services.Configure<NatsSettings>(options =>
{
    options.Url = builder.Configuration.GetValue<string>("NATS_URL", "nats://localhost:4222")!;
    options.QueueGroup = builder.Configuration.GetValue<string>("NATS_QUEUE_GROUP", "chathub-hub")!;
});



builder.Services.Configure<StorageSettings>(options =>
{
    options.Endpoint = builder.Configuration.GetValue<string>("S3_ENDPOINT", "http://localhost:9000")!;
    options.AccessKey = builder.Configuration.GetValue<string>("S3_ACCESS_KEY", "minioadmin")!;
    options.SecretKey = builder.Configuration.GetValue<string>("S3_SECRET_KEY", "minioadmin")!;
    options.Bucket = builder.Configuration.GetValue<string>("S3_BUCKET", "chathub-uploads")!;
    options.Region = builder.Configuration.GetValue<string>("S3_REGION", "us-east-1")!;
    options.ForcePathStyle = builder.Configuration.GetValue<bool>("S3_FORCE_PATH_STYLE", true);
});

// Register raw settings types for constructors that don't use IOptions<T>
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatHubSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NatsSettings>>().Value);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageSettings>>().Value);

// Add authentication
var jwtKey = builder.Configuration.GetValue<string>("JWT_SIGNING_KEY", "qwertyuiopasdfghjklzxcvbnm123456");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration.GetValue<string>("JWT_ISSUER", "ChatHub"),
            ValidAudience = builder.Configuration.GetValue<string>("JWT_AUDIENCE", "ChatHub"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // Allow JWT from WebSocket query parameter (?token=...)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                
                var accessToken = context.Request.Query["token"];
                Console.WriteLine(accessToken);
                
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add services
builder.Services.AddSingleton<IConnectionRegistry, ConnectionRegistry>();
builder.Services.AddSingleton<IWebSocketSender, WebSocketSender>();
builder.Services.AddSingleton<INatsBackplane>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NatsSettings>>().Value;
    var chatHubSettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatHubSettings>>().Value;
    return new NatsBackplane(settings, chatHubSettings.PodId);
});
builder.Services.AddSingleton<IMessageDispatcher, MessageDispatcher>();

builder.Services.AddSingleton<IRateLimiter, MongoDbRateLimiter>();
builder.Services.AddSingleton<IPresenceService, MongoDbPresenceService>();
builder.Services.AddSingleton<IBlobStorageClient, S3BlobStorageClient>();

builder.Services.AddSingleton<IMessageRepository, MessageRepository>();
builder.Services.AddSingleton<IConversationRepository, ConversationRepository>();

// Add voice messaging services
builder.Services.AddSingleton<VoiceSessionBuffer>();

// Add message handlers
builder.Services.AddScoped<JoinServiceHandler>();
builder.Services.AddScoped<LeaveServiceHandler>();
builder.Services.AddScoped<TextMessageHandler>();
builder.Services.AddScoped<DeliveredHandler>();
builder.Services.AddScoped<PongHandler>();
builder.Services.AddScoped<VoiceChunkHandler>();
builder.Services.AddScoped<VoiceMessageHandler>();
builder.Services.AddScoped<FileAttachmentHandler>();
builder.Services.AddScoped<TypingHandler>();

// Register handler interfaces
builder.Services.AddScoped<IJoinServiceHandler>(sp => sp.GetRequiredService<JoinServiceHandler>());
builder.Services.AddScoped<ILeaveServiceHandler>(sp => sp.GetRequiredService<LeaveServiceHandler>());
builder.Services.AddScoped<ITextMessageHandler>(sp => sp.GetRequiredService<TextMessageHandler>());
builder.Services.AddScoped<IAckHandler>(sp => sp.GetRequiredService<DeliveredHandler>());
builder.Services.AddScoped<IPongHandler>(sp => sp.GetRequiredService<PongHandler>());
builder.Services.AddScoped<IVoiceChunkHandler>(sp => sp.GetRequiredService<VoiceChunkHandler>());
builder.Services.AddScoped<IVoiceMessageHandler>(sp => sp.GetRequiredService<VoiceMessageHandler>());
builder.Services.AddScoped<IFileAttachmentHandler>(sp => sp.GetRequiredService<FileAttachmentHandler>());
builder.Services.AddScoped<ITypingHandler>(sp => sp.GetRequiredService<TypingHandler>());

// Add hosted services
builder.Services.AddSingleton<MongoInitializer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MongoInitializer>());
builder.Services.AddSingleton<MongoWriterService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MongoWriterService>());
builder.Services.AddHostedService<NatsSubscriberService>();
builder.Services.AddHostedService<VoiceSessionCleanupService>();

// Add metrics
builder.Services.AddSingleton<ChatMetrics>();
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("ChatHub");
        // metrics.AddPrometheusExporter(); // TODO: requires OpenTelemetry.Exporter.Prometheus.AspNetCore stable package
    });

// Add CORS
var allowedOrigins = builder.Configuration.GetValue<string>("CHATHUB_ALLOWED_ORIGINS", "")?
    .Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ChatHubCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            policy.AllowAnyOrigin();
        }
        
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add controllers
builder.Services.AddControllers();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb")
    .AddCheck<NatsHealthCheck>("nats");

var app = builder.Build();

// Configure middleware
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors("ChatHubCors");

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.UseAuthentication();
app.UseAuthorization();

// Map health checks
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => true
});

// Map Prometheus metrics
// app.MapPrometheusScrapingEndpoint();

// Map controllers
app.MapControllers();

// Map WebSocket middleware
app.UseChatHubWebSockets();

app.Run();
