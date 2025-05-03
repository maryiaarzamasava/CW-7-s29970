using Cwiczenia7.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cwiczenia7.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController(IDbService dbService) : ControllerBase
{
    //pobiera wszystkie wycieczki wraz z ich podstawowymi informacjami
    [HttpGet]
    public async Task<IActionResult> GetAllTrips()
    {
        return Ok(await dbService.GetTripsDetailsAsync());
    }
}