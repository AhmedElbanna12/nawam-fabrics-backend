using fabrics.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace fabrics.Services
{
    public class MessengerService
    {
        private readonly string _pageAccessToken;
        private readonly HttpClient _httpClient;

        public MessengerService(IConfiguration config, HttpClient httpClient)
        {
            _pageAccessToken = config["Messenger:PageAccessToken"];
            _httpClient = httpClient;
        }

        public async Task SendTextAsync(string recipientId, string text)
        {
            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { text }
            };

            await SendToMessenger(payload);
        }

        public async Task SendButtonsAsync(string recipientId, string text, List<Button> buttons)
        {
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
                            buttons = buttons
                        }
                    }
                }
            };

            await SendToMessenger(payload);
        }

        public async Task SendGenericTemplateAsync(string recipientId, List<GenericTemplateElement> elements)
        {
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
                            template_type = "generic",
                            elements = elements
                        }
                    }
                }
            };

            await SendToMessenger(payload);
        }

        private async Task SendToMessenger(object payload)
        {
            var url = $"https://graph.facebook.com/v18.0/me/messages?access_token={_pageAccessToken}";
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Messenger API Error: {response.StatusCode} - {error}");
            }
        }
    }
}