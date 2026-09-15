using MediRoute.HospitalService.Data;
using MediRoute.Shared.DTOs;
using MediRoute.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace MediRoute.HospitalService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AmbulancesController : ControllerBase
{
    private readonly MediRouteDbContext _db;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<AmbulancesController> _logger;

    public AmbulancesController(
        MediRouteDbContext db,
        ILogger<AmbulancesController> logger,
        IConnectionMultiplexer? redis = null)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Ambulance>>> GetAll()
        => Ok(await _db.Ambulances.ToListAsync());

    [HttpPost]
    public async Task<ActionResult<Ambulance>> Register([FromBody] Ambulance ambulance)
    {
        _db.Ambulances.Add(ambulance);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = ambulance.Id }, ambulance);
    }

    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation([FromBody] AmbulanceLocationUpdate update)
    {
        var ambulance = await _db.Ambulances.FindAsync(update.AmbulanceId);
        if (ambulance is null) return NotFound();

        ambulance.Latitude = update.Latitude;
        ambulance.Longitude = update.Longitude;
        ambulance.LastLocationUpdate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (_redis is not null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"ambulance:location:{update.AmbulanceId}";
                await db.StringSetAsync(key,
                    $"{update.Latitude},{update.Longitude}",
                    TimeSpan.FromMinutes(5));
                await db.PublishAsync(RedisChannel.Literal("ambulance:location"),
                    System.Text.Json.JsonSerializer.Serialize(update));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis location cache failed");
            }
        }

        return Ok(new { message = "Location updated", ambulance.Id, update.Latitude, update.Longitude });
    }

    [HttpGet("{id:guid}/location")]
    public async Task<IActionResult> GetLocation(Guid id)
    {
        if (_redis is not null)
        {
            try
            {
                var cached = await _redis.GetDatabase().StringGetAsync($"ambulance:location:{id}");
                if (cached.HasValue)
                {
                    var parts = cached.ToString().Split(',');
                    return Ok(new { ambulanceId = id, latitude = double.Parse(parts[0]), longitude = double.Parse(parts[1]), source = "cache" });
                }
            }
            catch { /* fall through to DB */ }
        }

        var ambulance = await _db.Ambulances.FindAsync(id);
        if (ambulance is null) return NotFound();
        return Ok(new { ambulanceId = id, latitude = ambulance.Latitude, longitude = ambulance.Longitude, source = "db" });
    }
}
