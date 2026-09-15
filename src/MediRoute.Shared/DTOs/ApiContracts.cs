using MediRoute.Shared.Enums;

namespace MediRoute.Shared.DTOs;

public record TriageRequest(
    string Symptoms,
    double Latitude,
    double Longitude,
    string? PatientName = null,
    string? ContactPhone = null,
    string? BloodTypeNeeded = null);

public record TriageResult(
    UrgencyLevel UrgencyLevel,
    string LikelyCondition,
    string SpecialistRequired,
    int EstimatedStabilizationMinutes,
    string Reasoning);

public record HospitalRecommendation(
    int HospitalId,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int AvailableBeds,
    int AvailableIcuBeds,
    double DistanceKm,
    double PredictedEtaMinutes,
    double Score,
    bool HasSpecialty,
    bool HasBloodType);

public record TriageResponse(
    TriageResult Triage,
    IReadOnlyList<HospitalRecommendation> RecommendedHospitals);

public record CapacityUpdateRequest(
    int HospitalId,
    int AvailableBeds,
    int AvailableIcuBeds,
    List<BloodStockDto>? BloodBank = null);

public record BloodStockDto(string BloodType, int UnitsAvailable);

public record AmbulanceLocationUpdate(
    Guid AmbulanceId,
    double Latitude,
    double Longitude);

public record HospitalCapacityEvent(
    int HospitalId,
    string HospitalName,
    int AvailableBeds,
    int AvailableIcuBeds,
    DateTime Timestamp);

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FullName, string Role);
public record AuthResponse(string Token, string Email, string FullName, string Role, DateTime ExpiresAt);
