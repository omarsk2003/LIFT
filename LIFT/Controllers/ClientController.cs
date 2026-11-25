using System.Security.Claims;
using LIFT.Models;
using LIFT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIFT.Controllers;

[Authorize(Roles = "client")]
public sealed class ClientController(IClientService service, IClientInteractionService interactions,
    IClientAiService ai, IPaymentCheckoutService checkout) : Controller
{
    private Guid CurrentId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet] public async Task<IActionResult> Index() => View(await service.GetDashboardAsync(CurrentId));
    [HttpGet] public async Task<IActionResult> Profile() => View(await service.GetProfileAsync(CurrentId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(ClientProfileForm form)
    {
        if (!ModelState.IsValid) return View("Profile", new ClientProfileVm { Email = User.FindFirstValue(ClaimTypes.Email) ?? "", Form = form });
        if (form.TrainingDays.Any(x => x is < 1 or > 7)) return BadRequest();
        await service.SaveProfileAsync(CurrentId, form);
        TempData["Success"] = "Profile saved.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet] public async Task<IActionResult> Workout() => View(await service.GetPlansAsync(CurrentId));
    [HttpGet] public async Task<IActionResult> Nutrition() => View(await service.GetPlansAsync(CurrentId));
    [HttpGet] public async Task<IActionResult> Progress() => View(await service.GetProgressAsync(CurrentId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddActivity(ClientActivityForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await service.AddActivityAsync(CurrentId, form)) return BadRequest("Activity date cannot be in the future.");
        TempData["Success"] = "Activity recorded.";
        return RedirectToAction(nameof(Progress));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMeasurement(ClientMeasurementForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await service.AddMeasurementAsync(CurrentId, form);
        TempData["Success"] = "Measurement recorded.";
        return RedirectToAction(nameof(Progress));
    }

    [HttpGet] public async Task<IActionResult> Notifications() => View(await service.GetNotificationsAsync(CurrentId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(Guid id)
    {
        if (!await service.MarkNotificationReadAsync(CurrentId, id)) return NotFound();
        return RedirectToAction(nameof(Notifications));
    }

    [HttpGet] public async Task<IActionResult> Messages(Guid? id)
        => View(await interactions.GetMessagesAsync(CurrentId, id));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenSpecialistThread(Guid specialistId)
    {
        var id = await interactions.OpenSpecialistThreadAsync(CurrentId, specialistId);
        return id is null ? NotFound() : RedirectToAction(nameof(Messages), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenSupportThread()
    {
        var id = await interactions.OpenSupportThreadAsync(CurrentId);
        return id is null ? NotFound() : RedirectToAction(nameof(Messages), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(ClientMessageForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await interactions.SendMessageAsync(CurrentId, form)) return NotFound();
        return RedirectToAction(nameof(Messages), new { id = form.ConversationId });
    }

    [HttpGet] public async Task<IActionResult> Meetings()
        => View(await interactions.GetMeetingsAsync(CurrentId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideMeeting(ClientMeetingDecisionForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await interactions.DecideMeetingAsync(CurrentId, form)) return BadRequest("Meeting cannot be changed.");
        return RedirectToAction(nameof(Meetings));
    }

    [HttpGet] public async Task<IActionResult> Subscription()
        => View(await interactions.GetSubscriptionAsync(CurrentId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCheckout(StartCheckoutForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var siteBase = new Uri($"{Request.Scheme}://{Request.Host}/");
        var result = await checkout.StartAsync(CurrentId, form, siteBase, cancellationToken);
        if (result.Success) return Redirect(result.ApprovalUrl!);
        TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Subscription));
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReturn([FromQuery] string token, CancellationToken cancellationToken)
    {
        var result = await checkout.CompleteAsync(CurrentId, token, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Subscription));
    }

    [HttpGet]
    public IActionResult PaymentCancelled()
    {
        TempData["Error"] = "PayPal checkout was cancelled. Your current plan is unchanged.";
        return RedirectToAction(nameof(Subscription));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestSpecialistChange(ClientChangeRequestForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await interactions.RequestSpecialistChangeAsync(CurrentId, form))
        {
            TempData["Error"] = "This change request could not be submitted.";
            return RedirectToAction(nameof(Subscription));
        }
        TempData["Success"] = "Change request sent to admin.";
        return RedirectToAction(nameof(Subscription));
    }

    [HttpGet] public async Task<IActionResult> AiAssistant(CancellationToken cancellationToken)
        => View(await ai.GetAsync(CurrentId, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AiChat(ClientAiChatForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await ai.ChatAsync(CurrentId, form, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "AI replied." : result.Error;
        return RedirectToAction(nameof(AiAssistant));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AiAnalyze(CancellationToken cancellationToken)
    {
        var result = await ai.AnalyzeAsync(CurrentId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Analysis completed." : result.Error;
        return RedirectToAction(nameof(AiAssistant));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AiGeneratePlan(ClientAiPlanForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await ai.GeneratePlanAsync(CurrentId, form, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "AI draft sent to your specialist for review." : result.Error;
        return RedirectToAction(nameof(AiAssistant));
    }
}
