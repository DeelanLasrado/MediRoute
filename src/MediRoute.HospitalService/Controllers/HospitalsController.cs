using System.Text;
using System.Text.Json;
using MediRoute.HospitalService.Data;
using MediRoute.Shared.DTOs;
using MediRoute.Shared.Events;
using MediRoute.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace MediRoute.HospitalService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HospitalsController : ControllerBase
{
    private readonly MediRouteDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<HospitalsController> _logger;

    public HospitalsController(MediRouteDbContext db, IConfiguration config, ILogger<HospitalsController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Hospital>>> GetAll([FromQuery] string? city = null)
    {
        var query = _db.Hospitals
            .Include(h => h.Specialties)
            .Include(h => h.BloodBank)
            .Where(h => h.IsActive);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(h => h.City == city);

        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Hospital>> GetById(int id)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Specialties)
            .Include(h => h.BloodBank)
            .FirstOrDefaultAsync(h => h.Id == id);

        return hospital is null ? NotFound() : Ok(hospital);
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<IEnumerable<object>>> GetNearby(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm = 50)
    {
        var hospitals = await _db.Hospitals
            .Include(h => h.Specialties)
            .Include(h => h.BloodBank)
            .Where(h => h.IsActive)
            .ToListAsync();

        var nearby = hospitals
            .Select(h => new
            {
                Hospital = h,
                DistanceKm = HaversineKm(lat, lng, h.Latitude, h.Longitude)
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .ToList();

        return Ok(nearby);
    }

    [Authorize(Roles = "HospitalAdmin,Admin")]
    [HttpPut("{id:int}/capacity")]
    public async Task<IActionResult> UpdateCapacity(int id, [FromBody] CapacityUpdateRequest request)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.BloodBank)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hospital is null) return NotFound();

        hospital.AvailableBeds = request.AvailableBeds;
        hospital.AvailableIcuBeds = request.AvailableIcuBeds;
        hospital.LastUpdated = DateTime.UtcNow;

        if (request.BloodBank is not null)
        {
            foreach (var stock in request.BloodBank)
            {
                var existing = hospital.BloodBank.FirstOrDefault(b => b.BloodType == stock.BloodType);
                if (existing is not null)
                {
                    existing.UnitsAvailable = stock.UnitsAvailable;
                    existing.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync();
        PublishCapacityEvent(hospital);
        _logger.LogInformation("Capacity updated for hospital {HospitalId}", id);

        return Ok(hospital);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Hospital>> Create([FromBody] Hospital hospital)
    {
        _db.Hospitals.Add(hospital);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = hospital.Id }, hospital);
    }

    private void PublishCapacityEvent(Hospital hospital)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                UserName = _config["RabbitMQ:Username"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest"
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare(QueueNames.CapacityEvents, durable: true, exclusive: false, autoDelete: false);

            var evt = new HospitalCapacityEvent(
                hospital.Id, hospital.Name, hospital.AvailableBeds,
                hospital.AvailableIcuBeds, DateTime.UtcNow);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
            channel.BasicPublish("", QueueNames.CapacityEvents, null, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish capacity event (RabbitMQ may be unavailable)");
        }
    }

    internal static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180;
}
