namespace fabrics.Models
{
    public class GenericTemplateElement
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string ImageUrl { get; set; }
        public List<Button> Buttons { get; set; } = new List<Button>();
    }
}
