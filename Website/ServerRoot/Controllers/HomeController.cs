using Microsoft.AspNetCore.Mvc;

namespace Fabric.Server
{
    [Route("/[action]")]
    public class HomeController : Controller
    {
        [Route("/")]
        public IActionResult Index() => View();
        public IActionResult Documentation() => View();
        public IActionResult Changelog() => View();
        public IActionResult Download() => View();
    }
}
