using Cwiczenia7.Exceptions;
using Cwiczenia7.Models.DTOs;
using Cwiczenia7.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cwiczenia7.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController(IDbService dbService) : ControllerBase
{
    //pobiera wszystkie wycieczki powiazane z konkretnym klientem o podanym id
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetTripByClientId([FromRoute] int id)
    {
        try
        {
            return Ok(await dbService.GetTripsByClientsIdAsync(id));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    //tworzy nowego klienta
    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] ClientCreateDTO body)
    {
        var client = await dbService.CreateClientAsync(body);
        return Created($"clients/{client.IdClient}", client);
    }

    //rejestruje klienta na konkretna wycieczke jesli klient i wycieczka istnieja i liczba miejsc nie zostala przekroczona
    [HttpPut("{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClientForTrip([FromRoute] int id,[FromRoute] int tripId)
    {
        try
        {
            await dbService.RegisterClientForTripAsync(id, tripId);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (TripFullException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    //usuwa rejestracje klienta z wycieczki jesli rejestracja istnieja
    [HttpDelete("{id}/trips/{tripId}")]
    public async Task<IActionResult> DeleteClientForTrip([FromRoute] int id, [FromRoute] int  tripId)
    {
        try
        {
            await dbService.RemoveRegistrationAsync(id, tripId);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}