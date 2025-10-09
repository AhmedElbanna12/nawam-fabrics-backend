using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace fabrics.Services
{
    public class MessengerService
    {
        private readonly string _pageAccessToken;
        private readonly IHttpClientFactory _httpClientFactory;

        public MessengerService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _pageAccessToken = config["Messenger:PageAccessToken"];
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendTextAsync(string recipientId, string text)
        {
            var client = _httpClientFactory.CreateClient();

            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { text }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await client.PostAsync($"https://graph.facebook.com/v18.0/me/messages?access_token={_pageAccessToken}", content);
        }

        public async Task SendButtonsAsync(string recipientId, string text, List<object> buttons)
        {
            var client = _httpClientFactory.CreateClient();

            var payload = new
            {
                recipient = new { id = recipientId },
                message = new
                {
                    attachment = new
                    {
                        type = "template",
                        payload = new
                        {
                            template_type = "button",
                            text,
                            buttons
                        }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await client.PostAsync($"https://graph.facebook.com/v18.0/me/messages?access_token={_pageAccessToken}", content);
        }
    }
}
