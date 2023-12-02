using Microsoft.AspNetCore.Mvc;

namespace UnitTestEx.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private int _state;

        public TestController Add(int value)
        {
            _state += value;
            return this;
        }

        [HttpGet()]
        public int Get()
        {
            return _state;
        }
    }
}