using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Market")]
    public class MarketApi : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public MarketApi(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return Ok();
        }

        [HttpGet("api/v1/item_display_attributes")]
        public IActionResult ItemDisplayAttributes()
        {
            return ReturnJsonFile("ItemDisplayAttributes.json");
        }

        [HttpGet("api/v1/market_categories")]
        public IActionResult MarketCategories()
        {
            return ReturnJsonFile("MarketCategories.json");
        }

        private IActionResult ReturnJsonFile(string fileName)
        {
            var path = Path.Combine(_env.ContentRootPath, "StaticData", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var json = System.IO.File.ReadAllText(path);

            using var _ = JsonDocument.Parse(json);
            return Content(json, "application/json");
        }
    }
}
