using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gym.Controllers;

[Authorize]
public class CompletionController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (TempData["RegistrationSuccess"] is not string message)
            return RedirectToAction("Index", "MainHomepage");
        ViewData["Message"] = message;
        ViewData["Title"] = TempData["CompletionTitle"] as string ?? "Submission complete";
        var destination = TempData["CompletionReturnUrl"] as string;
        ViewData["ReturnUrl"] = Url.IsLocalUrl(destination) ? destination : Url.Content("~/");
        ViewData["Warning"] = TempData["ErrorMessage"] as string;
        TempData.Remove("SuccessMessage");
        return View();
    }
}
