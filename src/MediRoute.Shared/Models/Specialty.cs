using System.Text.Json.Serialization;

namespace MediRoute.Shared.Models;

public class Specialty
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int HospitalId { get; set; }
    [JsonIgnore]
    public Hospital? Hospital { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int DoctorsOnDuty { get; set; }
}
