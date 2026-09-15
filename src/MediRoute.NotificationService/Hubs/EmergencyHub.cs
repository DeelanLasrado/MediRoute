using MediRoute.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace MediRoute.NotificationService.Hubs;

public class EmergencyHub : Hub
{
    public async Task JoinHospitalGroup(int hospitalId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"hospital-{hospitalId}");

    public async Task LeaveHospitalGroup(int hospitalId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hospital-{hospitalId}");

    public async Task JoinCityGroup(string city)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"city-{city.ToLowerInvariant()}");

    public async Task SubscribeToAmbulance(Guid ambulanceId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"ambulance-{ambulanceId}");
}

public static class HubEvents
{
    public const string CapacityUpdated = "CapacityUpdated";
    public const string AmbulanceLocationUpdated = "AmbulanceLocationUpdated";
    public const string EmergencyAlert = "EmergencyAlert";
    public const string RoutingAssigned = "RoutingAssigned";
}

public class NotificationBroadcaster
{
    private readonly IHubContext<EmergencyHub> _hub;

    public NotificationBroadcaster(IHubContext<EmergencyHub> hub) => _hub = hub;

    public Task BroadcastCapacityAsync(HospitalCapacityEvent evt)
        => _hub.Clients.All.SendAsync(HubEvents.CapacityUpdated, evt);

    public Task BroadcastToHospitalAsync(int hospitalId, string eventName, object payload)
        => _hub.Clients.Group($"hospital-{hospitalId}").SendAsync(eventName, payload);

    public Task BroadcastAmbulanceLocationAsync(AmbulanceLocationUpdate update)
        => _hub.Clients.Group($"ambulance-{update.AmbulanceId}")
            .SendAsync(HubEvents.AmbulanceLocationUpdated, update);
}
