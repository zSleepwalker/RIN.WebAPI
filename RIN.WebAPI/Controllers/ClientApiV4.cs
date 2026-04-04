using Microsoft.AspNetCore.Mvc;
using System;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Clientapi/api/v4")]
    public class ClientApiV4 : ControllerBase
    {
        [HttpGet("dashboard/conductor-assets")]
        public object ConductorAssets()
        {
            return new { };
        }

        [HttpGet("dashboard/conductor-events")]
        public object ConductorEvents()
        {
            return new { };
        }

        [HttpGet("gift_notifications/{characterId}")]
        public object GiftNotifications(long characterId)
        {
            return Array.Empty<object>();
        }
    }
}
