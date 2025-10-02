using fabrics.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AirtableService _air;
        public CategoriesController(AirtableService air) => _air = air;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _air.GetCategoriesAsync();
            return Ok(categories);

        }
    }
}
