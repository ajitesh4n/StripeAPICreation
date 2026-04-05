using Microsoft.AspNetCore.Mvc;

public class UserController : Controller
{
    public IActionResult Create()
    {
        return View();
    }
}