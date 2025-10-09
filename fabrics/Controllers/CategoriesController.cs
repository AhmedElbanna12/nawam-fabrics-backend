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
            _air = air ; 
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var allCategories = await _air.GetCategoriesAsync();
            var allProducts = await _air.GetProductsAsync();

            // Main Categories
            var mainCategories = allCategories
                .Where(c => string.IsNullOrEmpty(c.ParentCategory))
                .ToList();

            foreach (var mainCat in mainCategories)
            {
                // SubCategories لكل Main
                var subCats = allCategories
                    .Where(c => c.ParentCategory == mainCat.Id)
                    .ToList();

                foreach (var subCat in subCats)
                {
                    // ربط المنتجات لكل SubCategory
                    subCat.Products = allProducts
                        .Where(p => p.SubCategory == subCat.Id)
                        .ToList();
                }

                mainCat.SubCategory = subCats;
            }

            return Ok(mainCategories);
        }
    }
}
