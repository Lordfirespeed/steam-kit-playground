using AspNetEphemeralHttpServerPoC.Util;
using Microsoft.AspNetCore.Mvc;

namespace AspNetEphemeralHttpServerPoC.Controllers;


public class HomeController : ControllerBase
{
    [Route("/api")]
    public IActionResult Index() =>
        new StandardJsonResult(null) { StatusCode = 401 };
}
