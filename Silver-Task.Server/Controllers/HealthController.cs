using Microsoft.AspNetCore.Mvc;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "ok",
                timeUtc = DateTime.UtcNow
            });
        }
    }
}
