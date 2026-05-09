using ChatHub.Api.HealthChecks;
using ChatHub.Api.Handlers;
using ChatHub.Api.Middleware;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Auth;
using ChatHub.Infrastructure.Cache;
using ChatHub.Infrastructure.Nats;
using ChatHub.Infrastructure.Persistence;
using ChatHub.Infrastructure.Storage;
using ChatHub.Infrastructure.WebSockets;
using ChatHub.Infrastructure.Writers;
using MongoDB.Driver;
using NATS.Client;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add settings
builder.Services.Configure<ChatHubSettings>(
    builder.Configuration.GetSection(ChatHubSettings.SectionName));
builder.Services.Configure<NatsSettings>(
    builder.Configuration.GetSection(NatsSettings.SectionName));
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection(MongoSettings.SectionName));
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection(StorageSettings.SectionName));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection(RedisSettings.SectionName));

// Add MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoSettings>>().Value;
    return client.GetDatabase(settings.DatabaseName);
});

// Add Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisSettings>>().Value;
    return ConnectionMultiplexer.Connect(settings.ConnectionString);
});

// Add NATS
builder.Services.AddSingleton<IConnection>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NatsSettings>>().Value;
    var opts = ConnectionFactory.GetDefaultOptions();
    opts.Url = settings.Url;
    var factory = new ConnectionFactory();
    return factory.CreateConnection(opts);
});

// Add core services
builder.Services.AddSingleton<IConnectionRegistry, ConnectionRegistry>();
builder.Services.AddSingleton<IWebSocketSender, WebSocketSender>();
builder.Services.AddSingleton<INatsBackplane, NatsBackplane>();
builder.Services.AddSingleton<IMessageDispatcher, MessageDispatcher>();
builder.Services.AddSingleton<IJwtValidator, JwtValidator>();

// Add scoped repositories
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

// Add Redis services
builder.Services.AddSingleton<IPresenceService, RedisPresenceService>();
builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
builder.Services.AddSingleton<IVoiceSessionBuffer, RedisVoiceSessionBuffer>();

// Add storage
builder.Services.AddSingleton<IBlobStorageClient, S3BlobStorageClient>();

// Add hosted services
builder.Services.AddHostedService<MongoInitializer>();
builder.Services.AddHostedService<NatsSubscriberService>();
builder.Services.AddHostedService<MongoWriterService>();

// Add message handlers
builder.Services.AddScoped<IMessageHandler<JoinServiceMessage>, JoinServiceHandler>();
builder.Services.AddScoped<IMessageHandler<LeaveServiceMessage>, LeaveServiceHandler>();
builder.Services.AddScoped<IMessageHandler<TextMessage>, TextMessageHandler>();
builder.Services.AddScoped<IMessageHandler<TypingMessage>, TypingHandler>();
builder.Services.AddScoped<IMessageHandler<VoiceChunkMessage>, VoiceChunkHandler>();
builder.Services.AddScoped<IMessageHandler<VoiceMessage>, VoiceMessageHandler>();
builder.Services.AddScoped<IMessageHandler<FileAttachmentMessage>, FileAttachmentHandler>();
builder.Services.AddScoped<IMessageHandler<AckMessage>, AckHandler>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<NatsHealthCheck>("nats");

// Add controllers
builder.Services.AddControllers();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configure middleware
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(15)
});

app.UseChatHubWebSockets();

app.UseRouting();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
