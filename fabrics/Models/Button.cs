namespace fabrics.Models
{
    public class Button
    {
        public string Type { get; set; } = "postback";
        public string Title { get; set; }
        public string Payload { get; set; }
    }
}
