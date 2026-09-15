namespace MediRoute.Shared.Models;

public class Ambulance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VehicleNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string DriverPhone { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsAvailable { get; set; } = true;
    public Guid? ActiveRequestId { get; set; }
    public DateTime LastLocationUpdate { get; set; } = DateTime.UtcNow;
}
