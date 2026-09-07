using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;

namespace UltraSol.Modules.Catalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    internal class ProductController: ControllerBase
    {
        [HttpGet]
        public IActionResult Hello()
        {
            Console.Write("Test");
            return Ok("Oke Ní");
        }   
    }
}