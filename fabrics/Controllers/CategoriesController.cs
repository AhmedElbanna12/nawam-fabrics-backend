using fabrics.Services;
using Microsoft.AspNetCore.Mvc;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AirtableService _air;

        public CategoriesController(AirtableService air)
        {
            _air = air;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var allCategories = await _air.GetCategoriesAsync();
            var allProducts = await _air.GetProductsAsync();

            // بناء hierarchy
            var mainCategories = allCategories
                .Where(c => c["ParentCategory"] == null)
                .ToList();

            foreach (var mainCat in mainCategories)
            {
                var subCats = allCategories
                    .Where(c => c["ParentCategory"] is object parent && parent.ToString() == mainCat["Id"].ToString())
                    .ToList();

                foreach (var subCat in subCats)
                {
                    subCat["Products"] = allProducts
                        .Where(p => p["SubCategory"]?.ToString() == subCat["Id"].ToString())
                        .ToList();
                }

                mainCat["SubCategory"] = subCats;
            }

            return Ok(mainCategories);
        }
    }
}
