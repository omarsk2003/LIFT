using System.Security.Claims;
using LIFT.Models;
using LIFT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIFT.Controllers;

[Authorize(Roles = "coach")]
public sealed class CoachController(ICoachService service, ICoachWorkspaceService workspace) : Controller
{
    private Guid CurrentId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet] public async Task<IActionResult> Index() => View(await service.GetDashboardAsync(CurrentId));
    [HttpGet] public async Task<IActionResult> MyClients() => View(await service.GetDashboardAsync(CurrentId));
    [HttpGet] public async Task<IActionResult> Client(Guid id)
    {
        var result = await service.GetClientAsync(CurrentId, id);
        return result is null ? NotFound() : View(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlan(WorkoutPlanForm form)
    {
        if (!ModelState.IsValid)
        {
            var detail = await service.GetClientAsync(CurrentId, form.ClientId);
            if (detail is null) return NotFound();
            detail.Form = form;
            return View("Client", detail);
        }
        if (!await service.SavePlanAsync(CurrentId, form)) return NotFound();
        TempData["Success"] = form.Publish ? "Workout plan published." : "Workout draft saved.";
        return RedirectToAction(nameof(Client), new { id = form.ClientId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewAiPlan(CoachAiReviewForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await service.ReviewAiPlanAsync(CurrentId, form)) return NotFound();
        TempData["Success"] = form.Decision == "approve" ? "AI workout approved and published." : "AI workout rejected.";
        return RedirectToAction(nameof(MyClients));
    }

    [HttpGet] public async Task<IActionResult> Profile() => View(await workspace.GetProfileAsync(CurrentId));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(NutritionistProfileForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await workspace.UpdateProfileAsync(CurrentId, form);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet] public async Task<IActionResult> Guidance() => View(new CoachGuidancePageVm
    {
        Clients = (await service.GetDashboardAsync(CurrentId)).Clients,
        Notes = await workspace.GetGuidanceAsync(CurrentId)
    });
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGuidance(NutritionGuidanceForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await workspace.AddGuidanceAsync(CurrentId, form)) return NotFound();
        TempData["Success"] = "Exercise guidance saved.";
        return RedirectToAction(nameof(Guidance));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGuidance(Guid id)
    {
        if (!await workspace.DeleteGuidanceAsync(CurrentId, id)) return NotFound();
        return RedirectToAction(nameof(Guidance));
    }

    [HttpGet] public async Task<IActionResult> Messages(Guid? id) => View(await workspace.GetMessagesAsync(CurrentId, id));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenClientThread(Guid clientId)
    {
        var id = await workspace.OpenClientThreadAsync(CurrentId, clientId);
        return id is null ? NotFound() : RedirectToAction(nameof(Messages), new { id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenAdminThread()
    {
        var id = await workspace.OpenAdminThreadAsync(CurrentId);
        return id is null ? NotFound() : RedirectToAction(nameof(Messages), new { id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(NutritionMessageForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await workspace.SendMessageAsync(CurrentId, form)) return NotFound();
        return RedirectToAction(nameof(Messages), new { id = form.ConversationId });
    }

    [HttpGet] public async Task<IActionResult> Meetings() => View(new CoachMeetingPageVm
    {
        Clients = (await service.GetDashboardAsync(CurrentId)).Clients,
        Meetings = await workspace.GetMeetingsAsync(CurrentId)
    });
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMeeting(NutritionMeetingForm form)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var error = await workspace.CreateMeetingAsync(CurrentId, form);
        TempData[error is null ? "Success" : "Error"] = error ?? "Meeting proposed.";
        return RedirectToAction(nameof(Meetings));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMeeting(Guid id)
    {
        if (!await workspace.CancelMeetingAsync(CurrentId, id)) return NotFound();
        return RedirectToAction(nameof(Meetings));
    }
}
