using MediRoute.NotificationService.Hubs;
using MediRoute.NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();

var redisConn = builder.Configuration.GetConnectionString("Redis");
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redisConn) && !redisConn.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        signalRBuilder.AddStackExchangeRedis(redisConn, options =>
        {
            options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("MediRoute");
        });
    }
    catch
    {
        // Redis backplane optional for local/dev
    }
}

builder.Services.AddSingleton<NotificationBroadcaster>();
builder.Services.AddHostedService<CapacityEventConsumer>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "MediRoute Notification Service", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<EmergencyHub>("/hubs/emergency");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "NotificationService" }));

app.Run();
