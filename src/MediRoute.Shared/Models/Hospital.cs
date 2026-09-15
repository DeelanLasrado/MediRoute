namespace MediRoute.Shared.Models;

public class Hospital
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int TotalBeds { get; set; }
    public int AvailableBeds { get; set; }
    public int IcuBeds { get; set; }
    public int AvailableIcuBeds { get; set; }
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public List<Specialty> Specialties { get; set; } = [];
    public List<BloodBankStock> BloodBank { get; set; } = [];
}
