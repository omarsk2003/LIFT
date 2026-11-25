using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using LIFT.Models;
using LIFT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIFT.Controllers;

[Authorize(Roles = "admin")]
public sealed class AdminController(IAdminService adminService, IAdminCatalogService catalogService,
    IAdminSupportService supportService, IAdminAccountsService accountsService, IAdminChangesService changesService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int specialistPage = 1, int clientPage = 1,
        int subscriptionPage = 1, CancellationToken cancellationToken = default)
        => View(await adminService.GetDashboardAsync(search, specialistPage, clientPage, subscriptionPage,
            cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Clients(string? search, int page = 1, CancellationToken cancellationToken = default)
        => View(await adminService.GetDashboardAsync(search, 1, page, 1, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Specialists(string? search, int page = 1, CancellationToken cancellationToken = default)
        => View(await adminService.GetDashboardAsync(search, page, 1, 1, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> AdminChanges(CancellationToken cancellationToken)
        => View(await changesService.GetAsync(cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideChange(AdminChangeDecisionForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await changesService.DecideAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Change request reviewed.";
        return RedirectToAction(nameof(AdminChanges));
    }
    [HttpGet]
    public async Task<IActionResult> AdminAccounts(CancellationToken cancellationToken)
        => View(await accountsService.GetAsync(CurrentUserId(), cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var profile = await accountsService.GetProfileAsync(CurrentUserId(), cancellationToken);
        return profile is null ? NotFound() : View(profile);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024 + 4096)]
    public async Task<IActionResult> UpdateProfilePhoto(IFormFile? photo, CancellationToken cancellationToken)
    {
        var error = await accountsService.SavePhotoAsync(CurrentUserId(), photo, cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Profile photo updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdmin(CreateAdminForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await accountsService.CreateAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Admin account created.";
        return RedirectToAction(nameof(AdminAccounts));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdminStatus(AdminStatusForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await accountsService.SetStatusAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Admin status updated.";
        return RedirectToAction(nameof(AdminAccounts));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAdminPassword(AdminPasswordForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await accountsService.ChangePasswordAsync(form, CurrentUserId(), cancellationToken);
        if (error is not null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Profile));
        }
        await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }
    [HttpGet]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
    {
        var model = await adminService.GetReviewAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Certificate(Guid id, CancellationToken cancellationToken)
    {
        var certificate = await adminService.GetCertificateAsync(id, cancellationToken);
        return certificate is null ? NotFound() : File(certificate.Content, "application/pdf");
    }
    [HttpGet]
    public IActionResult SiteAppearance() => View();

    [HttpGet]
    public async Task<IActionResult> Subscriptions(CancellationToken cancellationToken)
        => View(await catalogService.GetAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Support(Guid? id, CancellationToken cancellationToken)
        => View(await supportService.GetAsync(CurrentUserId(), id, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetContactStatus(AdminContactStatusForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await supportService.SetContactStatusAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Contact message updated.";
        return RedirectToAction(nameof(Support));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(AdminReplyForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await supportService.ReplyAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Reply sent.";
        return RedirectToAction(nameof(Support), new { id = form.ConversationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSupport(AdminCloseThreadForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await supportService.CloseAsync(form.ConversationId, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Support conversation closed.";
        return RedirectToAction(nameof(Support), new { id = form.ConversationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlanPrice(AdminPriceForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await catalogService.UpdatePriceAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Plan price updated for new subscriptions.";
        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePromo(AdminPromoForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await catalogService.SavePromoAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Promo code saved.";
        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePromo(AdminPromoToggleForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var error = await catalogService.TogglePromoAsync(form.Id, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Promo status updated.";
        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewSpecialist(AdminSpecialistDecisionForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await adminService.ReviewSpecialistAsync(form, CurrentUserId(), cancellationToken);
        TempData[result is null ? "Success" : "Error"] = result ?? "Specialist application updated.";
        return RedirectToAction(nameof(Specialists));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAccountStatus(AdminAccountStatusForm form, string? returnTo, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await adminService.ChangeAccountStatusAsync(form, CurrentUserId(), cancellationToken);
        TempData[result is null ? "Success" : "Error"] = result ?? "Account status updated.";
        return RedirectToAction(returnTo == "specialists" ? nameof(Specialists) : nameof(Clients));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditClient(AdminClientEditForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Check the client information and try again.";
            return RedirectToAction(nameof(Clients));
        }
        var error = await adminService.EditClientAsync(form, CurrentUserId(), cancellationToken);
        TempData[error is null ? "Success" : "Error"] = error ?? "Client information updated.";
        return RedirectToAction(nameof(Clients));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

