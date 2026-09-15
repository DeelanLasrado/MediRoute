using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using MediRoute.Shared.DTOs;
using MediRoute.Shared.Enums;
using OpenAI.Chat;

namespace MediRoute.TriageService.Services;

public interface IAzureOpenAIService
{
    Task<TriageResult> AnalyzeSymptomsAsync(string symptoms);
}

public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly bool _useMock;

    public AzureOpenAIService(IConfiguration config, ILogger<AzureOpenAIService> logger)
    {
        _config = config;
        _logger = logger;
        var endpoint = _config["AzureOpenAI:Endpoint"];
        var key = _config["AzureOpenAI:ApiKey"];
        _useMock = string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key)
                   || key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TriageResult> AnalyzeSymptomsAsync(string symptoms)
    {
        if (_useMock)
        {
            _logger.LogWarning("Azure OpenAI not configured — using rule-based mock triage");
            return MockTriage(symptoms);
        }

        try
        {
            var client = new AzureOpenAIClient(
                new Uri(_config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(_config["AzureOpenAI:ApiKey"]!));

            var deployment = _config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
            var chatClient = client.GetChatClient(deployment);

            var systemPrompt = """
                You are a medical triage AI for emergency healthcare routing in India.
                Analyze patient symptoms and respond ONLY with valid JSON (no markdown):
                {
                  "urgency": "P1" | "P2" | "P3",
                  "likely_condition": "string",
                  "specialist_needed": "string",
                  "estimated_stabilization_time": number (minutes),
                  "reasoning": "brief explanation"
                }
                P1 = immediate life threat (cardiac arrest, severe trauma, stroke, major hemorrhage)
                P2 = urgent within hours (fractures, moderate breathing difficulty, high fever with confusion)
                P3 = non-urgent (minor cuts, mild symptoms)
                specialist_needed should be one of: Cardiology, Neurology, Trauma, Emergency Medicine, Orthopedics, Pediatrics, General Surgery
                """;

            var response = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"Patient symptoms: {symptoms}")
            ]);

            var content = response.Value.Content[0].Text.Trim();
            if (content.StartsWith("```"))
            {
                content = content.Replace("```json", "").Replace("```", "").Trim();
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var urgencyStr = root.GetProperty("urgency").GetString() ?? "P2";
            var urgency = urgencyStr.ToUpperInvariant() switch
            {
                "P1" => UrgencyLevel.P1,
                "P3" => UrgencyLevel.P3,
                _ => UrgencyLevel.P2
            };

            return new TriageResult(
                urgency,
                root.GetProperty("likely_condition").GetString() ?? "Unknown",
                root.GetProperty("specialist_needed").GetString() ?? "Emergency Medicine",
                root.TryGetProperty("estimated_stabilization_time", out var est) ? est.GetInt32() : 30,
                root.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI triage failed — falling back to mock");
            return MockTriage(symptoms);
        }
    }

    private static TriageResult MockTriage(string symptoms)
    {
        var lower = symptoms.ToLowerInvariant();

        if (ContainsAny(lower, "chest pain", "heart attack", "cardiac", "unconscious", "not breathing",
                "severe bleeding", "stroke", "paralysis", "seizure", "gunshot", "stab"))
        {
            var specialist = ContainsAny(lower, "chest", "heart", "cardiac") ? "Cardiology"
                : ContainsAny(lower, "stroke", "paralysis", "seizure") ? "Neurology"
                : "Trauma";
            return new TriageResult(UrgencyLevel.P1,
                "Critical emergency — possible life-threatening condition",
                specialist, 15,
                "Keywords indicate potential P1 emergency requiring immediate specialist care.");
        }

        if (ContainsAny(lower, "fracture", "broken", "breathing", "fever", "vomit", "accident",
                "burn", "allergic", "pregnancy", "labor"))
        {
            var specialist = ContainsAny(lower, "fracture", "broken") ? "Orthopedics"
                : ContainsAny(lower, "pregnancy", "labor") ? "Emergency Medicine"
                : "Emergency Medicine";
            return new TriageResult(UrgencyLevel.P2,
                "Urgent condition requiring prompt medical attention",
                specialist, 45,
                "Symptoms suggest P2 urgency — patient should reach care within hours.");
        }

        return new TriageResult(UrgencyLevel.P3,
            "Non-urgent medical concern",
            "Emergency Medicine", 90,
            "Symptoms appear non-critical; standard emergency evaluation recommended.");
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(text.Contains);
}
