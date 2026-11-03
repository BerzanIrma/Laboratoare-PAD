
using HotelApi.Models;
using HotelApi.Services;
using Microsoft.AspNetCore.Mvc;

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
        [FromQuery] string? city = null)
    {
        if (offset < 0 || limit < 1 || limit > 100)
            return BadRequest("Invalid pagination parameters.");

        var result = await _hotelService.GetAllAsync(
            offset, limit, city);

        Response.Headers["X-Data-Source"] = result.Source;

        return Ok(result.Hotels);
    }

    // GET /hotels/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
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

        if (string.IsNullOrWhiteSpace(hotel.Name) ||
            hotel.Name.Length > 100 ||
            string.IsNullOrWhiteSpace(hotel.City) ||
            hotel.Stars < 1 || hotel.Stars > 5 ||
            hotel.TotalRooms <= 0 ||
            hotel.PricePerNight <= 0)
        {
            return BadRequest("Invalid hotel data.");
        }

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
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(hotel.Name) ||
            hotel.Name.Length > 100 ||
            string.IsNullOrWhiteSpace(hotel.City) ||
            hotel.Stars < 1 || hotel.Stars > 5 ||
            hotel.TotalRooms <= 0 ||
            hotel.PricePerNight <= 0)
        {
            return BadRequest("Invalid hotel data.");
        }

        var updated = await _hotelService.UpdateAsync(id, hotel);

        if (!updated)
            return NotFound("Hotel not found.");

        return NoContent();
    }

    // DELETE /hotels/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _hotelService.DeleteAsync(id);

        if (!deleted)
            return NotFound("Hotel not found.");

        return NoContent();
    }
}
