//using fabrics.Models;
//using Microsoft.Extensions.Configuration;
//using System.Text;
//using System.Text.Json;

//namespace fabrics.Services
//{
//    public class MessengerService
//    {
//        private readonly string _pageAccessToken;
//        private readonly HttpClient _httpClient;

//        public MessengerService(IConfiguration config, HttpClient httpClient)
//        {
//            _pageAccessToken = config["Messenger:PageAccessToken"];
//            _httpClient = httpClient;
//        }

//        public async Task SendTextAsync(string recipientId, string text)
//        {
//            var payload = new
//            {
//                recipient = new { id = recipientId },
//                message = new { text }
//            };

//            await SendToMessenger(payload);
//        }

//        public async Task SendButtonsAsync(string recipientId, string text, List<Button> buttons)
//        {
//            try
//            {
//                // ✅ لا يزيد عن 3 أزرار في القالب الواحد
//                var limitedButtons = buttons.Take(3).ToList();

//                var payload = new
//                {
//                    recipient = new { id = recipientId },
//                    message = new
//                    {
//                        attachment = new
//                        {
//                            type = "template",
//                            payload = new
//                            {
//                                template_type = "button",
//                                text = text,
//                                buttons = limitedButtons
//                            }
//                        }
//                    }
//                };

//                await SendToMessenger(payload);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error sending buttons: {ex.Message}");
//            }
//        }

//        public async Task SendGenericTemplateAsync(string recipientId, List<GenericTemplateElement> elements)
//        {
//            try
//            {
//                var payload = new
//                {
//                    recipient = new { id = recipientId },
//                    message = new
//                    {
//                        attachment = new
//                        {
//                            type = "template",
//                            payload = new
//                            {
//                                template_type = "generic",
//                                elements = elements.Take(10).ToList() // ✅ لا يزيد عن 10 عناصر
//                            }
//                        }
//                    }
//                };

//                await SendToMessenger(payload);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error sending generic template: {ex.Message}");
//            }
//        }

//        private async Task SendToMessenger(object payload)
//        {
//            var url = $"https://graph.facebook.com/v18.0/me/messages?access_token={_pageAccessToken}";
//            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            var content = new StringContent(json, Encoding.UTF8, "application/json");

//            var response = await _httpClient.PostAsync(url, content);

//            if (!response.IsSuccessStatusCode)
//            {
//                var error = await response.Content.ReadAsStringAsync();
//                Console.WriteLine($"Messenger API Error: {response.StatusCode} - {error}");
//            }
//        }
//    }
//}