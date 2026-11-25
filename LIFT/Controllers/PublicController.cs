using LIFT.Models;
using LIFT.Services;
using Microsoft.AspNetCore.Mvc;

namespace LIFT.Controllers;

public sealed class PublicController(IPublicContactService contactService) : Controller
{
    [HttpGet]
    public IActionResult Landing() => View();

    [HttpGet]
    public IActionResult HowItWorks() => View();

    [HttpGet]
    public IActionResult About() => View();

    [HttpGet]
    public IActionResult Contact() => View(new ContactForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(form);
        await contactService.SaveAsync(form, cancellationToken);
        TempData["ContactSuccess"] = "Your message was received. The LIFT team will review it.";
        return RedirectToAction(nameof(Contact));
    }
}


