using Microsoft.AspNetCore.Mvc;

namespace PxApi.Controllers
{
    [Route("api/v1/meta")]
    [ApiController]
    public class MetadataControllerV1 : ControllerBase
    {
        // GET api/v1/meta/{path}
        [HttpGet("{path}")]
        public IActionResult GetMetadataById([FromRoute] string path)
        {
            return Ok();
        }
    }
}
