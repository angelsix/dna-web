using Microsoft.AspNetCore.Mvc;

namespace Fabric.Modern1.Server
{
    public class DownloadController : Controller
    {
        [Route("/download/win32")]
        public IActionResult DownloadWindows32() => File("/Assets/Releases/DnaWeb-win32.zip", "application/x-zip-compressed", "DnaWeb.zip");

        [Route("/download/win64")]
        public IActionResult DownloadWindows64() => File("/Assets/Releases/DnaWeb-win64.zip", "application/x-zip-compressed", "DnaWeb.zip");

        [Route("/api/releases")]
        public IActionResult Releases() => File("/Assets/Releases/releases.json", "application/json");
    }
}