using fabrics.Dtos;
using fabrics.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationsController : ControllerBase
    {
        private readonly AirtableService _air;
        public ReservationsController(AirtableService air) => _air = air;

        [HttpPost]
        [Consumes("multipart/form-data")]

        public async Task<IActionResult> Create([FromForm] CreateReservationDto dto)
        {
            var recordId = await _air.CreateReservationAsync(dto);
            return Ok(new { ReservationId = recordId });
        }
    }
}

