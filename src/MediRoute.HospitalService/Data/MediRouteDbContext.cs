using MediRoute.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediRoute.HospitalService.Data;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public int? HospitalId { get; set; }
}

public class MediRouteDbContext : IdentityDbContext<ApplicationUser>
{
    public MediRouteDbContext(DbContextOptions<MediRouteDbContext> options) : base(options) { }

    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<BloodBankStock> BloodBankStocks => Set<BloodBankStock>();
    public DbSet<EmergencyRequest> EmergencyRequests => Set<EmergencyRequest>();
    public DbSet<Ambulance> Ambulances => Set<Ambulance>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Hospital>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.Name).HasMaxLength(200).IsRequired();
            e.Property(h => h.City).HasMaxLength(100);
            e.HasMany(h => h.Specialties).WithOne(s => s.Hospital).HasForeignKey(s => s.HospitalId);
            e.HasMany(h => h.BloodBank).WithOne(b => b.Hospital).HasForeignKey(b => b.HospitalId);
        });

        builder.Entity<Specialty>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(100).IsRequired();
        });

        builder.Entity<BloodBankStock>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BloodType).HasMaxLength(5).IsRequired();
            e.HasIndex(b => new { b.HospitalId, b.BloodType }).IsUnique();
        });

        builder.Entity<EmergencyRequest>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.AssignedHospital).WithMany().HasForeignKey(r => r.AssignedHospitalId);
        });

        builder.Entity<Ambulance>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.VehicleNumber).HasMaxLength(50).IsRequired();
        });

        SeedData(builder);
    }

    private static void SeedData(ModelBuilder builder)
    {
        var seedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var hospitals = new[]
        {
            new Hospital
            {
                Id = 1, Name = "AIIMS Delhi", Address = "Ansari Nagar, New Delhi", City = "Delhi",
                Latitude = 28.5672, Longitude = 77.2100, TotalBeds = 500, AvailableBeds = 42,
                IcuBeds = 80, AvailableIcuBeds = 8, Phone = "+91-11-26588500", LastUpdated = seedTime
            },
            new Hospital
            {
                Id = 2, Name = "Apollo Hospitals Chennai", Address = "Greams Road, Chennai", City = "Chennai",
                Latitude = 13.0604, Longitude = 80.2496, TotalBeds = 350, AvailableBeds = 28,
                IcuBeds = 50, AvailableIcuBeds = 5, Phone = "+91-44-28290200", LastUpdated = seedTime
            },
            new Hospital
            {
                Id = 3, Name = "Fortis Hospital Mumbai", Address = "Mulund West, Mumbai", City = "Mumbai",
                Latitude = 19.1726, Longitude = 72.9560, TotalBeds = 300, AvailableBeds = 15,
                IcuBeds = 40, AvailableIcuBeds = 2, Phone = "+91-22-67994444", LastUpdated = seedTime
            },
            new Hospital
            {
                Id = 4, Name = "Manipal Hospital Bangalore", Address = "Old Airport Road, Bangalore", City = "Bangalore",
                Latitude = 12.9581, Longitude = 77.6482, TotalBeds = 400, AvailableBeds = 55,
                IcuBeds = 60, AvailableIcuBeds = 12, Phone = "+91-80-25024444", LastUpdated = seedTime
            },
            new Hospital
            {
                Id = 5, Name = "Medanta Gurugram", Address = "Sector 38, Gurugram", City = "Gurugram",
                Latitude = 28.4397, Longitude = 77.0405, TotalBeds = 450, AvailableBeds = 33,
                IcuBeds = 70, AvailableIcuBeds = 6, Phone = "+91-124-4141414", LastUpdated = seedTime
            }
        };

        builder.Entity<Hospital>().HasData(hospitals);

        var specialties = new List<Specialty>();
        var id = 1;
        var dutyCounts = new[] { 2, 3, 1, 4, 2, 3 };
        string[] commonSpecs = ["Cardiology", "Neurology", "Trauma", "Emergency Medicine", "Orthopedics", "Pediatrics"];
        foreach (var h in hospitals)
        {
            for (var i = 0; i < commonSpecs.Length; i++)
            {
                specialties.Add(new Specialty
                {
                    Id = id++,
                    Name = commonSpecs[i],
                    HospitalId = h.Id,
                    IsAvailable = true,
                    DoctorsOnDuty = dutyCounts[i]
                });
            }
        }
        builder.Entity<Specialty>().HasData(specialties);

        var bloodTypes = new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };
        var stockUnits = new[] { 12, 5, 18, 3, 8, 2, 25, 7 };
        var bloodStocks = new List<BloodBankStock>();
        id = 1;
        foreach (var h in hospitals)
        {
            for (var i = 0; i < bloodTypes.Length; i++)
            {
                bloodStocks.Add(new BloodBankStock
                {
                    Id = id++,
                    HospitalId = h.Id,
                    BloodType = bloodTypes[i],
                    UnitsAvailable = Math.Max(0, stockUnits[i] - h.Id),
                    LastUpdated = seedTime
                });
            }
        }
        builder.Entity<BloodBankStock>().HasData(bloodStocks);
    }
}
