using MediRoute.RoutingService.Services;
using MediRoute.Shared.DTOs;
using MediRoute.Shared.Enums;
using MediRoute.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace MediRoute.RoutingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutingController : ControllerBase
{
    private readonly IEtaPredictionService _etaService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RoutingController> _logger;

    public RoutingController(
        IEtaPredictionService etaService,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<RoutingController> logger)
    {
        _etaService = etaService;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    [HttpGet("recommend")]
    public async Task<ActionResult<IReadOnlyList<HospitalRecommendation>>> Recommend(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string specialist = "Emergency Medicine",
        [FromQuery] int urgency = 2,
        [FromQuery] string? bloodType = null)
    {
        var hospitals = await FetchHospitalsAsync(lat, lng);
        if (hospitals.Count == 0)
            return Ok(Array.Empty<HospitalRecommendation>());

        var urgencyLevel = (UrgencyLevel)urgency;
        var recommendations = hospitals.Select(h =>
        {
            var distance = HaversineKm(lat, lng, h.Latitude, h.Longitude);
            var load = 1f - (float)h.AvailableBeds / Math.Max(1, h.TotalBeds);
            var traffic = EstimateTrafficFactor();
            var eta = _etaService.PredictEta((float)distance, traffic, load);

            var hasSpecialty = h.Specialties.Any(s =>
                s.IsAvailable && s.Name.Equals(specialist, StringComparison.OrdinalIgnoreCase));
            var hasBlood = string.IsNullOrEmpty(bloodType) ||
                           h.BloodBank.Any(b => b.BloodType == bloodType && b.UnitsAvailable > 0);

            var availability = urgencyLevel == UrgencyLevel.P1
                ? (h.AvailableIcuBeds > 0 ? 1.0 : 0.1) * (h.AvailableBeds + 1)
                : (h.AvailableBeds + 1.0);

            var specialtyMatch = hasSpecialty ? 1.5 : 0.3;
            if (hasBlood) specialtyMatch *= 1.2;

            var score = (availability * specialtyMatch) / Math.Max(1.0, eta);

            return new HospitalRecommendation(
                h.Id, h.Name, h.Address, h.Latitude, h.Longitude,
                h.AvailableBeds, h.AvailableIcuBeds,
                Math.Round(distance, 2), Math.Round(eta, 1),
                Math.Round(score, 4), hasSpecialty, hasBlood);
        })
        .OrderByDescending(r => r.Score)
        .Take(3)
        .ToList();

        return Ok(recommendations);
    }

    [HttpPost("train")]
    public IActionResult RetrainModel()
    {
        _etaService.TrainModel();
        return Ok(new { message = "ETA model retrained successfully" });
    }

    [HttpGet("eta")]
    public ActionResult<object> PredictEta(
        [FromQuery] float distanceKm,
        [FromQuery] float trafficFactor = 1.2f,
        [FromQuery] float hospitalLoad = 0.5f)
    {
        var eta = _etaService.PredictEta(distanceKm, trafficFactor, hospitalLoad);
        return Ok(new { distanceKm, trafficFactor, hospitalLoad, predictedEtaMinutes = eta });
    }

    private async Task<List<Hospital>> FetchHospitalsAsync(double lat, double lng)
    {
        try
        {
            var hospitalBase = _config["Services:Hospital"] ?? "http://localhost:5002";
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{hospitalBase}/api/hospitals/nearby?lat={lat}&lng={lng}&radiusKm=50");

            if (!response.IsSuccessStatusCode)
            {
                // Fallback: get all
                response = await client.GetAsync($"{hospitalBase}/api/hospitals");
                if (!response.IsSuccessStatusCode) return [];
                return await response.Content.ReadFromJsonAsync<List<Hospital>>() ?? [];
            }

            var nearby = await response.Content.ReadFromJsonAsync<List<NearbyHospitalDto>>();
            return nearby?.Select(n => n.Hospital).Where(h => h is not null).Cast<Hospital>().ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch hospitals from Hospital Service");
            return GetFallbackHospitals();
        }
    }

    private static List<Hospital> GetFallbackHospitals() =>
    [
        new()
        {
            Id = 1, Name = "AIIMS Delhi", Address = "Ansari Nagar, New Delhi",
            Latitude = 28.5672, Longitude = 77.2100, TotalBeds = 500, AvailableBeds = 42,
            IcuBeds = 80, AvailableIcuBeds = 8,
            Specialties = [new() { Name = "Cardiology", IsAvailable = true }, new() { Name = "Trauma", IsAvailable = true }, new() { Name = "Emergency Medicine", IsAvailable = true }],
            BloodBank = [new() { BloodType = "O+", UnitsAvailable = 20 }, new() { BloodType = "A+", UnitsAvailable = 10 }]
        },
        new()
        {
            Id = 5, Name = "Medanta Gurugram", Address = "Sector 38, Gurugram",
            Latitude = 28.4397, Longitude = 77.0405, TotalBeds = 450, AvailableBeds = 33,
            IcuBeds = 70, AvailableIcuBeds = 6,
            Specialties = [new() { Name = "Cardiology", IsAvailable = true }, new() { Name = "Neurology", IsAvailable = true }, new() { Name = "Emergency Medicine", IsAvailable = true }],
            BloodBank = [new() { BloodType = "O+", UnitsAvailable = 15 }, new() { BloodType = "B+", UnitsAvailable = 8 }]
        }
    ];

    private static float EstimateTrafficFactor()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 8 and <= 10 => 2.0f,
            >= 17 and <= 20 => 2.2f,
            >= 12 and <= 14 => 1.4f,
            _ => 1.0f
        };
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private record NearbyHospitalDto(Hospital Hospital, double DistanceKm);
}
