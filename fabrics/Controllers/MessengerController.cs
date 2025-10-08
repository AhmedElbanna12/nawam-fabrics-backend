using fabrics.Services;
using Microsoft.AspNetCore.Http;
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

        // ✅ Verification endpoint (GET)
        [HttpGet("webhook")]
        public IActionResult VerifyMessengerWebhook([FromQuery] string hub_mode, [FromQuery] string hub_verify_token, [FromQuery] string hub_challenge)
        {
            const string VERIFY_TOKEN = "your-verify-token"; // نفس اللي حطيته في Meta Developer

            if (hub_mode == "subscribe" && hub_verify_token == VERIFY_TOKEN)
            {
                return Ok(hub_challenge);
            }

            return Unauthorized();
        }

        // ✅ Receive messages (POST)
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveMessengerMessage([FromBody] JsonElement body)
        {
            await _messengerService.HandleMessageAsync(body);
            return Ok();
        }
    }
}

