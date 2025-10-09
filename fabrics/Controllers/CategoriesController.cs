using fabrics.Models;
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
            var allCategoriesDict = await _air.GetCategoriesAsync(); // بيرجع List<Dictionary<string, object>>

            // تحويل الـ Dictionary لكائن Category
            var allCategories = allCategoriesDict.Select(d => new Category
            {
                Id = d["Id"].ToString(),
                Name = d["Name"].ToString(),
                Description = d.ContainsKey("Description") ? d["Description"]?.ToString() : null,
                ParentCategory = d.ContainsKey("ParentCategory") ? d["ParentCategory"]?.ToString() : null
            }).ToList();

            // بناء hierarchy
            var mainCategories = allCategories.Where(c => c.ParentCategory == null).ToList();
            foreach (var mainCat in mainCategories)
            {
                mainCat.SubCategories = allCategories
                    .Where(c => c.ParentCategory == mainCat.Id)
                    .ToList();
            }

            return Ok(mainCategories);
        }
    }
}
