using fabrics.Models;

namespace fabrics.Services.Interface
{
    public interface IAirtableService
    {
        Task<List<Category>> GetAllCategoriesAsync();
        Task<List<Category>> GetMainCategoriesAsync();
        Task<List<Category>> GetSubCategoriesAsync(string parentCategoryId);
        Task<List<Product>> GetProductsByCategoryAsync(string categoryId);
    }
}
