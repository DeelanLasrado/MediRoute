using MediRoute.NotificationService.Hubs;
using MediRoute.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace MediRoute.NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationBroadcaster _broadcaster;

    public NotificationsController(NotificationBroadcaster broadcaster) => _broadcaster = broadcaster;

    [HttpPost("capacity")]
    public async Task<IActionResult> PushCapacity([FromBody] HospitalCapacityEvent evt)
    {
        await _broadcaster.BroadcastCapacityAsync(evt);
        return Ok(new { message = "Capacity broadcast sent" });
    }

    [HttpPost("ambulance-location")]
    public async Task<IActionResult> PushAmbulanceLocation([FromBody] AmbulanceLocationUpdate update)
    {
        await _broadcaster.BroadcastAmbulanceLocationAsync(update);
        return Ok(new { message = "Ambulance location broadcast sent" });
    }

    [HttpPost("emergency-alert")]
    public async Task<IActionResult> PushEmergencyAlert([FromBody] object alert)
    {
        await _broadcaster.BroadcastToHospitalAsync(
            alert is System.Text.Json.JsonElement je && je.TryGetProperty("hospitalId", out var id)
                ? id.GetInt32() : 0,
            HubEvents.EmergencyAlert, alert);
        return Ok(new { message = "Emergency alert sent" });
    }
}
