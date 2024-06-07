using APBD.Data;
using APBD.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APBD.Controllrs;

[ApiController]
[Route("api/[Controller]")]
public class TripsController : ControllerBase
{
    private readonly ApbdContext _context;

    public TripsController(ApbdContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrips([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var totalTrips = await _context.Trips.CountAsync();

        var trips = await _context.Trips
            .OrderByDescending(t => t.DateFrom)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Name,
                e.Description,
                DateFrom = e.DateFrom.ToString("yyyy-MM-dd"),
                DateTo = e.DateTo.ToString("yyyy-MM-dd"),
                e.MaxPeople,
                Countries = e.IdCountries.Select(c => new
                {
                    c.Name
                }),
                Clients = e.ClientTrips.Select(ct => new
                {
                    ct.IdClientNavigation.FirstName,
                    ct.IdClientNavigation.LastName
                })
            })
            .ToListAsync();

        var result = new
        {
            PageNum = page,
            PageSize = pageSize,
            AllPages = (int)Math.Ceiling(totalTrips / (double)pageSize),
            Trips = trips
        };

        return Ok(result);
    }
    
    [HttpDelete("{idClient}")]
    public async Task<IActionResult> DeleteClient(int idClient)
    {
        var client = await _context.Clients
            .Include(c => c.ClientTrips)
            .FirstOrDefaultAsync(c => c.IdClient == idClient);

        if (client == null)
        {
            return NotFound(new { message = "Client not found" });
        }

        if (client.ClientTrips.Any())
        {
            return BadRequest(new { message = "Client has assigned trips and cannot be deleted" });
        }

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();

        return NoContent();
    }
    
    
        [HttpPost("{idTrip}/clients")]
        public async Task<IActionResult> AssignClientToTrip(int idTrip, [FromBody] ClientTripDTO request)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.IdTrip == idTrip && t.DateFrom > DateTime.Now);
            if (trip == null)
            {
                return BadRequest(new { message = "Trip does not exist or it is in the past" });
            }
            
            var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.Pesel == request.Pesel);
            if (existingClient != null)
            {
                var clientTrip = await _context.ClientTrips.FirstOrDefaultAsync(ct => ct.IdClient == existingClient.IdClient && ct.IdTrip == idTrip);
                if (clientTrip != null)
                {
                    return BadRequest(new { message = "Client is already assigned to this trip" });
                }
            }
            
            if (existingClient == null)
            {
                existingClient = new Client
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    Telephone = request.Telephone,
                    Pesel = request.Pesel
                };
                _context.Clients.Add(existingClient);
                await _context.SaveChangesAsync();
            }
            
            var newClientTrip = new ClientTrip
            {
                IdClient = existingClient.IdClient,
                IdTrip = idTrip,
                RegisteredAt = DateTime.Now,
                PaymentDate = request.PaymentDate
            };
            _context.ClientTrips.Add(newClientTrip);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTrips), new { idTrip = idTrip }, new { message = "Client assigned to trip successfully" });
        }

}