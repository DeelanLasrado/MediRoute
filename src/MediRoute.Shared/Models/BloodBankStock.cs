using System.Text.Json.Serialization;

namespace MediRoute.Shared.Models;

public class BloodBankStock
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    [JsonIgnore]
    public Hospital? Hospital { get; set; }
    public string BloodType { get; set; } = string.Empty; // A+, A-, B+, B-, AB+, AB-, O+, O-
    public int UnitsAvailable { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
