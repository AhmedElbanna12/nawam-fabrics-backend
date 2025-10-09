namespace fabrics.Dtos
{
    public class CategoryTreeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<CategoryTreeDto> SubCategories { get; set; } = new();
    }
}
