using MediRoute.HospitalService.Data;
using MediRoute.Shared.Enums;
using MediRoute.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediRoute.HospitalService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmergenciesController : ControllerBase
{
    private readonly MediRouteDbContext _db;

    public EmergenciesController(MediRouteDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmergencyRequest>>> GetAll([FromQuery] RequestStatus? status = null)
    {
        var query = _db.EmergencyRequests.Include(e => e.AssignedHospital).AsQueryable();
        if (status.HasValue) query = query.Where(e => e.Status == status);
        return Ok(await query.OrderByDescending(e => e.RequestedAt).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmergencyRequest>> GetById(Guid id)
    {
        var req = await _db.EmergencyRequests.Include(e => e.AssignedHospital).FirstOrDefaultAsync(e => e.Id == id);
        return req is null ? NotFound() : Ok(req);
    }

    [HttpPost]
    public async Task<ActionResult<EmergencyRequest>> Create([FromBody] EmergencyRequest request)
    {
        request.Id = Guid.NewGuid();
        request.RequestedAt = DateTime.UtcNow;
        request.Status = RequestStatus.Pending;
        _db.EmergencyRequests.Add(request);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] RequestStatus status)
    {
        var req = await _db.EmergencyRequests.FindAsync(id);
        if (req is null) return NotFound();
        req.Status = status;
        await _db.SaveChangesAsync();
        return Ok(req);
    }

    [HttpPut("{id:guid}/assign/{hospitalId:int}")]
    public async Task<IActionResult> AssignHospital(Guid id, int hospitalId)
    {
        var req = await _db.EmergencyRequests.FindAsync(id);
        if (req is null) return NotFound();
        var hospital = await _db.Hospitals.FindAsync(hospitalId);
        if (hospital is null) return NotFound(new { message = "Hospital not found" });

        req.AssignedHospitalId = hospitalId;
        req.Status = RequestStatus.Routed;
        await _db.SaveChangesAsync();
        return Ok(req);
    }
}
