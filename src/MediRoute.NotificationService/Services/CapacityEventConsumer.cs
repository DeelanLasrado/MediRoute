using System.Text;
using System.Text.Json;
using MediRoute.NotificationService.Hubs;
using MediRoute.Shared.DTOs;
using MediRoute.Shared.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MediRoute.NotificationService.Services;

public class CapacityEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<CapacityEventConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public CapacityEventConsumer(IServiceProvider services, IConfiguration config, ILogger<CapacityEventConsumer> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                UserName = _config["RabbitMQ:Username"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest",
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(QueueNames.CapacityEvents, durable: true, exclusive: false, autoDelete: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, args) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(args.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<HospitalCapacityEvent>(json);
                    if (evt is not null)
                    {
                        using var scope = _services.CreateScope();
                        var broadcaster = scope.ServiceProvider.GetRequiredService<NotificationBroadcaster>();
                        await broadcaster.BroadcastCapacityAsync(evt);
                        _logger.LogInformation("Broadcast capacity update for hospital {Id}", evt.HospitalId);
                    }
                    _channel.BasicAck(args.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing capacity event");
                    _channel?.BasicNack(args.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(QueueNames.CapacityEvents, autoAck: false, consumer);
            _logger.LogInformation("RabbitMQ capacity consumer started");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable — capacity consumer not started. Will rely on direct API pushes.");
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
