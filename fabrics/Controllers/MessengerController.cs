using fabrics.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessengerController : ControllerBase
    {
        private readonly MessengerService _messengerService;

        public MessengerController(MessengerService messengerService)
        {
            _messengerService = messengerService;
        }

        // ✅ 1. Webhook Verification (GET)
        [HttpGet("webhook")]
        public IActionResult VerifyMessengerWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string verifyToken,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            const string VERIFY_TOKEN = "my_messenger_token"; // لازم نفس التوكن اللي في Meta Developer

            if (mode == "subscribe" && verifyToken == VERIFY_TOKEN)
            {
                Console.WriteLine("✅ Messenger webhook verified successfully!");
                return Ok(challenge);
            }

            Console.WriteLine("❌ Messenger webhook verification failed.");
            return Forbid();
        }

        // ✅ 2. Receive Webhook Events (POST)
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveMessengerMessage([FromBody] JsonElement body)
        {
            if (body.ValueKind == JsonValueKind.Undefined || body.ValueKind == JsonValueKind.Null)
            {
                Console.WriteLine("❌ Empty webhook body received.");
                return BadRequest("Empty body");
            }

            Console.WriteLine("📩 Incoming Messenger webhook:");
            Console.WriteLine(body.ToString());

            try
            {
                // Handle message (لو عندك منطق معين)
                await _messengerService.HandleMessageAsync(body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error handling message: {ex.Message}");
            }

            return Ok(); // لازم ترد بـ 200 OK عشان Facebook ما يعيدش الإرسال
        }
    }
}
