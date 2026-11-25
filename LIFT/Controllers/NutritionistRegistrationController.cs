using LIFT.Models;
using LIFT.Services;
using Microsoft.AspNetCore.Mvc;

namespace LIFT.Controllers;

public sealed class NutritionistRegistrationController(ISpecialistRegistrationService service) : Controller
{
    [HttpGet]
    public IActionResult Apply() => View(new SpecialistRegistrationForm());

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(55_000_000)]
    public async Task<IActionResult> Apply(SpecialistRegistrationForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(form);
        var error = await service.RegisterAsync(form, "nutritionist", cancellationToken);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(form);
        }
        return RedirectToAction(nameof(Submitted));
    }

    [HttpGet]
    public IActionResult Submitted() => View();
}
