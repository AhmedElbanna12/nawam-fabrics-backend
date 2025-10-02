using fabrics.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class productsController : ControllerBase
    {
        private readonly AirtableService _air;
        public productsController(AirtableService air) => _air = air;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _air.GetProductsAsync();
            return Ok(products);
        }
    }
}
