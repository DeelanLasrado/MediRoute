using MediRoute.Shared.DTOs;
using MediRoute.TriageService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediRoute.TriageService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TriageController : ControllerBase
{
    private readonly IAzureOpenAIService _aiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<TriageController> _logger;

    public TriageController(
        IAzureOpenAIService aiService,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<TriageController> logger)
    {
        _aiService = aiService;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] TriageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symptoms))
            return BadRequest(new { message = "Symptoms description is required" });

        var triageResult = await _aiService.AnalyzeSymptomsAsync(request.Symptoms);

        IReadOnlyList<HospitalRecommendation> hospitals = [];
        try
        {
            var routingBase = _config["Services:Routing"] ?? "http://localhost:5003";
            var client = _httpClientFactory.CreateClient();
            var url = $"{routingBase}/api/routing/recommend" +
                      $"?lat={request.Latitude}&lng={request.Longitude}" +
                      $"&specialist={Uri.EscapeDataString(triageResult.SpecialistRequired)}" +
                      $"&urgency={(int)triageResult.UrgencyLevel}" +
                      $"&bloodType={Uri.EscapeDataString(request.BloodTypeNeeded ?? "")}";

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                hospitals = await response.Content.ReadFromJsonAsync<List<HospitalRecommendation>>() ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Routing service unavailable — returning triage only");
        }

        return Ok(new TriageResponse(triageResult, hospitals));
    }

    [HttpPost("analyze-only")]
    public async Task<ActionResult<TriageResult>> AnalyzeOnly([FromBody] TriageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symptoms))
            return BadRequest(new { message = "Symptoms description is required" });

        return Ok(await _aiService.AnalyzeSymptomsAsync(request.Symptoms));
    }
}
