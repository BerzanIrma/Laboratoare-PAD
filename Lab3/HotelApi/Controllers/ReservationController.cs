
using HotelApi.Models;
using HotelApi.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace HotelApi.Controllers;

[ApiController]
[Route("hotels/{hotelId}/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly ReservationService _service;
    private readonly ShardingService _sharding;

    public ReservationsController(
        ReservationService service,
        ShardingService sharding)
    {
        _service = service;
        _sharding = sharding;
    }

    // GET /hotels/{hotelId}/reservations
    [HttpGet]
    public async Task<IActionResult> GetAll(
        string hotelId,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 10)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            offset < 0 || limit < 1 || limit > 100)
            return BadRequest("Invalid parameters.");

        var reservations = await _service.GetAllAsync(
            hotelId, offset, limit);

        Response.Headers["X-Shard"] =
            _sharding.GetShard(hotelId);

        return Ok(reservations);
    }

    // GET /hotels/{hotelId}/reservations/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string hotelId, string id)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            !ObjectId.TryParse(id, out _))
            return BadRequest("Invalid ID.");

        var reservation = await _service.GetByIdAsync(
            hotelId, id);

        if (reservation == null)
            return NotFound("Reservation not found.");

        Response.Headers["X-Shard"] =
            _sharding.GetShard(hotelId);

        return Ok(reservation);
    }

    // POST /hotels/{hotelId}/reservations
    [HttpPost]
    public async Task<IActionResult> Create(
        string hotelId, Reservation reservation)
    {
        if (!ObjectId.TryParse(hotelId, out _))
            return BadRequest("Invalid HotelId.");

        if (!ModelState.IsValid ||
            !IsValidReservation(reservation))
            return BadRequest("Invalid reservation data.");

        if (!await _service.HotelExistsAsync(hotelId))
            return NotFound("Hotel not found.");

        reservation.HotelId = hotelId;

        var id = await _service.CreateAsync(reservation);

        reservation.Id = id;

        Response.Headers["X-Shard"] =
            _sharding.GetShard(hotelId);

        return CreatedAtAction(
            nameof(GetById),
            new { hotelId, id },
            reservation);
    }

    // PUT /hotels/{hotelId}/reservations/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string hotelId,
        string id,
        Reservation reservation)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            !ObjectId.TryParse(id, out _))
            return BadRequest("Invalid ID.");

        if (!ModelState.IsValid ||
            !IsValidReservation(reservation) ||
            (reservation.Status != "confirmed" &&
             reservation.Status != "cancelled"))
            return BadRequest("Invalid reservation data.");

        var updated = await _service.UpdateAsync(
            hotelId, id, reservation);

        if (!updated)
            return NotFound("Reservation not found.");

        Response.Headers["X-Shard"] =
            _sharding.GetShard(hotelId);

        return NoContent();
    }

    // DELETE /hotels/{hotelId}/reservations/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string hotelId, string id)
    {
        if (!ObjectId.TryParse(hotelId, out _) ||
            !ObjectId.TryParse(id, out _))
            return BadRequest("Invalid ID.");

        var cancelled = await _service.CancelAsync(
            hotelId, id);

        if (!cancelled)
            return NotFound("Reservation not found.");

        Response.Headers["X-Shard"] =
            _sharding.GetShard(hotelId);

        return NoContent();
    }

    private static bool IsValidReservation(Reservation reservation)
    {
        return !string.IsNullOrWhiteSpace(reservation.GuestName)
            && !string.IsNullOrWhiteSpace(reservation.GuestEmail)
            && new EmailAddressAttribute().IsValid(reservation.GuestEmail)
            && reservation.CheckOut > reservation.CheckIn
            && reservation.Rooms >= 1
            && reservation.Rooms <= 10;
    }
}
