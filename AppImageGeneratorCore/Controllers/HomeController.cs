using Microsoft.AspNetCore.Mvc;

namespace WAT.WebUI.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/
        public ActionResult Index() => View();
	}
}