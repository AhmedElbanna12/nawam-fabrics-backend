namespace fabrics.Models
{
    public class Category
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentCategory { get; set; } // ID للـ paren
        public string Description { get; set; }
        public List<Category> SubCategories { get; set; } = new List<Category>();
        public List<Product> Products { get; set; } = new List<Product>(); // لو عايز تضيف المنتجات
    }
}
