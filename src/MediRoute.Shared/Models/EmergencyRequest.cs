using System.Text.Json.Serialization;
using MediRoute.Shared.Enums;

namespace MediRoute.Shared.Models;

public class EmergencyRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SymptomsDescription { get; set; } = string.Empty;
    public UrgencyLevel Urgency { get; set; }
    public string AiDiagnosis { get; set; } = string.Empty;
    public string SpecialistRequired { get; set; } = string.Empty;
    public int EstimatedStabilizationMinutes { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int? AssignedHospitalId { get; set; }
    [JsonIgnore]
    public Hospital? AssignedHospital { get; set; }
    public string? PatientName { get; set; }
    public string? ContactPhone { get; set; }
    public string? BloodTypeNeeded { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
}
