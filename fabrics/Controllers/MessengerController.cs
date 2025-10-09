using fabrics.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace fabrics.Controllers
{
    [Route("api/messenger/webhook")]
    [ApiController]
    public class MessengerController : ControllerBase
    {
        private readonly MessengerService _messenger;
        private readonly AirtableService _airtable;

        public MessengerController(MessengerService messenger, AirtableService airtable)
        {
            _messenger = messenger;
            _airtable = airtable;
        }

        [HttpGet]
        public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                   [FromQuery(Name = "hub.verify_token")] string verifyToken,
                                   [FromQuery(Name = "hub.challenge")] string challenge)
        {
            const string VERIFY_TOKEN = "my_messenger_token";

            if (mode == "subscribe" && verifyToken == VERIFY_TOKEN)
            {
                Console.WriteLine("Webhook verified successfully!");
                return Ok(challenge);
            }
            else
            {
                return Forbid();
            }
        }


        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] JsonElement body)
        {
            var entry = body.GetProperty("entry")[0];
            var messaging = entry.GetProperty("messaging")[0];
            var senderId = messaging.GetProperty("sender").GetProperty("id").GetString();

            if (messaging.TryGetProperty("message", out var message))
            {
                // start or any text → show parent categories
                var (parents, _) = await _airtable.GetCategoriesAsync();
                var buttons = parents.Select(p => (object)new
                {
                    type = "postback",
                    title = p.Name,
                    payload = $"PARENT_{p.Id}"
                }).ToList();


                await _messenger.SendButtonsAsync(senderId, "اختر نوع القماش:", buttons);
            }
            else if (messaging.TryGetProperty("postback", out var postback))
            {
                var payload = postback.GetProperty("payload").GetString();

                if (payload.StartsWith("PARENT_"))
                {
                    var parentId = payload.Replace("PARENT_", "");
                    var (_, subs) = await _airtable.GetCategoriesAsync();

                    var subForParent = subs.Where(s => s.ParentCategoryIds.Contains(parentId)).ToList();
                    var buttons = subForParent.Select(s => new
                    {
                        type = "postback",
                        title = s.Name,
                        payload = $"SUB_{parentId}_{s.Id}"
                    }).Cast<object>().ToList();

                    await _messenger.SendButtonsAsync(senderId, "اختر النوع الفرعي:", buttons);


                }
                else if (payload.StartsWith("SUB_"))
                {
                    var parts = payload.Split('_');
                    var parentId = parts[1];
                    var subId = parts[2];

                    var products = await _airtable.GetProductsAsync();
                    var filtered = products
                        .Where(p => (string)p["MainCategoryId"] == parentId && (string)p["SubCategoryId"] == subId)
                        .ToList();

                    var text = filtered.Count > 0
                        ? string.Join("\n", filtered.Select(p => $"📦 {p["Name"]} - {p["PricePerMeter"]} جنيه"))
                        : "❌ لا توجد منتجات في هذا القسم.";

                    await _messenger.SendTextAsync(senderId, text);
                }
            }

            return Ok();
        }
    }
}
