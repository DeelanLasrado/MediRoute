using System.Net.Http.Headers;
using System.Net.Http.Json;
using MediRoute.Shared.DTOs;
using MediRoute.Shared.Models;

namespace MediRoute.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public string? FullName { get; private set; }
    public string? Role { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public AuthService(HttpClient http) => _http = http;

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        if (!response.IsSuccessStatusCode) return false;

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth is null) return false;

        Token = auth.Token;
        Email = auth.Email;
        FullName = auth.FullName;
        Role = auth.Role;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return true;
    }

    public void Logout()
    {
        Token = null;
        Email = null;
        FullName = null;
        Role = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }
}

public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http) => _http = http;

    public Task<List<Hospital>?> GetHospitalsAsync(string? city = null)
        => _http.GetFromJsonAsync<List<Hospital>>(
            string.IsNullOrEmpty(city) ? "api/hospitals" : $"api/hospitals?city={Uri.EscapeDataString(city)}");

    public Task<Hospital?> GetHospitalAsync(int id)
        => _http.GetFromJsonAsync<Hospital>($"api/hospitals/{id}");

    public async Task<bool> UpdateCapacityAsync(int hospitalId, CapacityUpdateRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/hospitals/{hospitalId}/capacity", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<TriageResponse?> AnalyzeSymptomsAsync(TriageRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/triage/analyze", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TriageResponse>();
    }

    public Task<List<EmergencyRequest>?> GetEmergenciesAsync()
        => _http.GetFromJsonAsync<List<EmergencyRequest>>("api/emergencies");

    public Task<List<Ambulance>?> GetAmbulancesAsync()
        => _http.GetFromJsonAsync<List<Ambulance>>("api/ambulances");

    public async Task<Ambulance?> RegisterAmbulanceAsync(Ambulance ambulance)
    {
        var response = await _http.PostAsJsonAsync("api/ambulances", ambulance);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Ambulance>();
    }
}
