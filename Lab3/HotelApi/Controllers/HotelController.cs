
using HotelApi.Models;
using HotelApi.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace HotelApi.Controllers;

[ApiController]
[Route("hotels")]
public class HotelsController : ControllerBase
{
    private readonly HotelService _hotelService;

    public HotelsController(HotelService hotelService)
    {
        _hotelService = hotelService;
    }

    // GET /hotels
[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] int offset = 0,
    [FromQuery] int limit = 10,
    [FromQuery] string? city = null,
    [FromQuery] int delayMs = 0)
{
    if (offset < 0 || limit < 1 || limit > 100)
        return BadRequest("Invalid pagination parameters.");

    if (delayMs < 0 || delayMs > 30000)
        return BadRequest("Invalid delayMs. It must be between 0 and 30000.");

    // Întârziere intenționată pentru testarea timeout/Circuit Breaker
    if (delayMs > 0)
    {
        await Task.Delay(delayMs);
    }

    var result = await _hotelService.GetAllAsync(
        offset, limit, city);

    Response.Headers["X-Data-Source"] = result.Source;

    return Ok(result.Hotels);
}

    // GET /hotels/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (!ObjectId.TryParse(id, out _))
            return BadRequest("Invalid hotel ID.");

        var result = await _hotelService.GetByIdAsync(id);

        Response.Headers["X-Data-Source"] = result.Source;

        if (result.Hotel == null)
            return NotFound("Hotel not found.");

        return Ok(result.Hotel);
    }

    // POST /hotels
    [HttpPost]
    public async Task<IActionResult> Create(Hotel hotel)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!IsValidHotel(hotel))
            return BadRequest("Invalid hotel data.");

        var id = await _hotelService.CreateAsync(hotel);

        hotel.Id = id;

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            hotel);
    }

    // PUT /hotels/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id, Hotel hotel)
    {
        if (!ObjectId.TryParse(id, out _))
            return BadRequest("Invalid hotel ID.");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!IsValidHotel(hotel))
            return BadRequest("Invalid hotel data.");

        var updated = await _hotelService.UpdateAsync(id, hotel);

        if (!updated)
            return NotFound("Hotel not found.");

        return NoContent();
    }

    // DELETE /hotels/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ObjectId.TryParse(id, out _))
            return BadRequest("Invalid hotel ID.");

        var deleted = await _hotelService.DeleteAsync(id);

        if (!deleted)
            return NotFound("Hotel not found.");

        return NoContent();
    }

    private static bool IsValidHotel(Hotel hotel)
    {
        return !string.IsNullOrWhiteSpace(hotel.Name)
            && hotel.Name.Length <= 100
            && !string.IsNullOrWhiteSpace(hotel.City)
            && hotel.Stars >= 1
            && hotel.Stars <= 5
            && hotel.TotalRooms > 0
            && hotel.PricePerNight > 0;
    }
}
