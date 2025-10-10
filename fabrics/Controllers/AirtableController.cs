using fabrics.Dtos;
using fabrics.Models;
using fabrics.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AirtableController : ControllerBase
    {
        private readonly IAirtableService _airtableService;
        private readonly ILogger<AirtableController> _logger;

        public AirtableController(IAirtableService airtableService, ILogger<AirtableController> logger)
        {
            _airtableService = airtableService;
            _logger = logger;
        }

        // ✅ جلب كل التصنيفات
        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponse<List<Category>>>> GetAllCategories()
        {
            try
            {
                var categories = await _airtableService.GetAllCategoriesAsync();

                return Ok(new ApiResponse<List<Category>>
                {
                    Success = true,
                    Message = "تم جلب التصنيفات بنجاح",
                    Data = categories
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب التصنيفات");
                return StatusCode(500, new ApiResponse<List<Category>>
                {
                    Success = false,
                    Message = "حدث خطأ في جلب التصنيفات"
                });
            }
        }

        // ✅ جلب التصنيفات الرئيسية فقط
        [HttpGet("categories/main")]
        public async Task<ActionResult<ApiResponse<List<Category>>>> GetMainCategories()
        {
            try
            {
                var mainCategories = await _airtableService.GetMainCategoriesAsync();

                return Ok(new ApiResponse<List<Category>>
                {
                    Success = true,
                    Message = "تم جلب التصنيفات الرئيسية بنجاح",
                    Data = mainCategories
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب التصنيفات الرئيسية");
                return StatusCode(500, new ApiResponse<List<Category>>
                {
                    Success = false,
                    Message = "حدث خطأ في جلب التصنيفات الرئيسية"
                });
            }
        }

        // ✅ جلب التصنيفات الفرعية لتصنيف معين
        [HttpGet("categories/{parentCategoryId}/subcategories")]
        public async Task<ActionResult<ApiResponse<List<Category>>>> GetSubCategories(string parentCategoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(parentCategoryId))
                {
                    return BadRequest(new ApiResponse<List<Category>>
                    {
                        Success = false,
                        Message = "معرف التصنيف الرئيسي مطلوب"
                    });
                }

                var subCategories = await _airtableService.GetSubCategoriesAsync(parentCategoryId);

                return Ok(new ApiResponse<List<Category>>
                {
                    Success = true,
                    Message = "تم جلب التصنيفات الفرعية بنجاح",
                    Data = subCategories
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب التصنيفات الفرعية للتصنيف {ParentCategoryId}", parentCategoryId);
                return StatusCode(500, new ApiResponse<List<Category>>
                {
                    Success = false,
                    Message = "حدث خطأ في جلب التصنيفات الفرعية"
                });
            }
        }

        // ✅ جلب المنتجات حسب التصنيف
        [HttpGet("products/category/{categoryId}")]
        public async Task<ActionResult<ApiResponse<List<Product>>>> GetProductsByCategory(string categoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(categoryId))
                {
                    return BadRequest(new ApiResponse<List<Product>>
                    {
                        Success = false,
                        Message = "معرف التصنيف مطلوب"
                    });
                }

                var products = await _airtableService.GetProductsByCategoryAsync(categoryId);

                return Ok(new ApiResponse<List<Product>>
                {
                    Success = true,
                    Message = "تم جلب المنتجات بنجاح",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب المنتجات للتصنيف {CategoryId}", categoryId);
                return StatusCode(500, new ApiResponse<List<Product>>
                {
                    Success = false,
                    Message = "حدث خطأ في جلب المنتجات"
                });
            }
        }

       
        // ✅ جلب كل المنتجات
        [HttpGet("products")]
        public async Task<ActionResult<ApiResponse<List<Product>>>> GetAllProducts()
        {
            try
            {
                // تحسين الأداء: استخدام الطريقة المباشرة بدلاً من التكرار
                var allCategories = await _airtableService.GetAllCategoriesAsync();
                var allProducts = new List<Product>();

                // نأخذ فقط الفئات التي لديها منتجات
                var categoriesWithProducts = allCategories.Where(c => c.ProductsCount > 0).ToList();

                foreach (var category in categoriesWithProducts)
                {
                    var products = await _airtableService.GetProductsByCategoryAsync(category.Id);
                    allProducts.AddRange(products);
                }

                // إزالة التكرارات
                var distinctProducts = allProducts.DistinctBy(p => p.Id).ToList();

                return Ok(new ApiResponse<List<Product>>
                {
                    Success = true,
                    Message = "تم جلب كل المنتجات بنجاح",
                    Data = distinctProducts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب كل المنتجات");
                return StatusCode(500, new ApiResponse<List<Product>>
                {
                    Success = false,
                    Message = "حدث خطأ في جلب المنتجات"
                });
            }
        }

        // ✅ جلب الهيكل الشجري للتصنيفات (للبوت)
        [HttpGet("categories/tree")]
        public async Task<ActionResult<ApiResponse<List<CategoryTreeDto>>>> GetCategoryTree()
        {
            try
            {
                var allCategories = await _airtableService.GetAllCategoriesAsync();
                var categoryTree = BuildCategoryTree(allCategories);

                return Ok(new ApiResponse<List<CategoryTreeDto>>
                {
                    Success = true,
                    Message = "تم جلب هيكل التصنيفات بنجاح",
                    Data = categoryTree
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في بناء هيكل التصنيفات");
                return StatusCode(500, new ApiResponse<List<CategoryTreeDto>>
                {
                    Success = false,
                    Message = "حدث خطأ في بناء هيكل التصنيفات"
                });
            }
        }

        
        // ✅ إندبوينت للحصول على فئة محددة بالID
        [HttpGet("categories/{categoryId}")]
        public async Task<ActionResult<ApiResponse<Category>>> GetCategoryById(string categoryId)
        {
            try
            {
                if (string.IsNullOrEmpty(categoryId))
                {
                    return BadRequest(new ApiResponse<Category>
                    {
                        Success = false,
                        Message = "معرف التصنيف مطلوب"
                    });
                }

                var allCategories = await _airtableService.GetAllCategoriesAsync();
                var category = allCategories.FirstOrDefault(c => c.Id == categoryId);

                if (category == null)
                {
                    return NotFound(new ApiResponse<Category>
                    {
                        Success = false,
                        Message = "التصنيف غير موجود"
                    });
                }

                return Ok(new ApiResponse<Category>
                {
                    Success = true,
                    Message = "تم جلب التصنيف بنجاح",
                    Data = category
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب التصنيف {CategoryId}", categoryId);
                return StatusCode(500, new ApiResponse<Category>
                {
                    Success = false,
                    Message = "حدث خطأ في جلب التصنيف"
                });
            }
        }

        // ✅ دالة مساعدة لبناء الهيكل الشجري
        private List<CategoryTreeDto> BuildCategoryTree(List<Category> categories)
        {
            var mainCategories = categories.Where(c => c.IsMainCategory).ToList();
            var tree = new List<CategoryTreeDto>();

            foreach (var mainCat in mainCategories)
            {
                var mainCategoryDto = new CategoryTreeDto
                {
                    Id = mainCat.Id,
                    Name = mainCat.Name,
                    ProductsCount = mainCat.ProductsCount
                };

                // إيجاد التصنيفات الفرعية
                var subCategories = categories.Where(c =>
                    c.ParentCategory != null &&
                    c.ParentCategory.Contains(mainCat.Id))
                    .ToList();

                foreach (var subCat in subCategories)
                {
                    mainCategoryDto.SubCategories.Add(new CategoryTreeDto
                    {
                        Id = subCat.Id,
                        Name = subCat.Name,
                        ProductsCount = subCat.ProductsCount
                    });
                }

                tree.Add(mainCategoryDto);
            }

            return tree;
        }
    }
}