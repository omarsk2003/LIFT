using System.Security.Claims;
using LIFT.Data;
using LIFT.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Controllers;

public sealed class AccountController(LiftDbContext db, IPasswordHasher<LiftUser> passwordHasher) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var email = form.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email))
        {
            ModelState.AddModelError(nameof(form.Email), "An account with this email already exists.");
            return View(form);
        }

        var freePlan = await db.SubscriptionPlans.SingleAsync(x => x.Code == "free");
        var user = new LiftUser
        {
            Id = Guid.NewGuid(), FullName = form.FullName.Trim(), Email = email,
            Role = "client", Status = "active", CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, form.Password);
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.Add(user);
        db.ClientProfiles.Add(new ClientProfile { UserId = user.Id });
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(), ClientId = user.Id, PlanId = freePlan.Id,
            Status = "active", PriceAtStart = 0, Currency = "USD", StartsAt = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(nameof(form.Email), "This email is already in use.");
            return View(form);
        }
        await transaction.CommitAsync();
        await SignInUser(user, "free");
        return RedirectToAction(nameof(Continue));
    }

    [HttpGet]
    public IActionResult Login() => View(new LoginForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var email = form.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, form.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "The email or password doesn't match.");
            return View(form);
        }
        if (user.Status != "active")
        {
            ModelState.AddModelError(string.Empty, "This account is not active. Contact LIFT Admin.");
            return View(form);
        }
        var plan = user.Role == "client"
            ? await db.Subscriptions.Where(x => x.ClientId == user.Id && x.Status == "active")
                .Join(db.SubscriptionPlans, x => x.PlanId, x => x.Id, (s, p) => p.Code).SingleOrDefaultAsync() ?? "free"
            : "";
        await SignInUser(user, plan);
        return RedirectToAction(nameof(Continue));
    }

    [HttpGet]
    public IActionResult Continue()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(Login));
        var destination = User.FindFirstValue(ClaimTypes.Role) switch
        {
            "coach" => "/Coach",
            "nutritionist" => "/Nutritionist",
            "admin" => "/Admin/Dashboard/dashboard.html",
            _ => "/Client"
        };
        return View((object)destination);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> LogoutPage()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return View();
    }

    private async Task SignInUser(LiftUser user, string plan)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("auth_version", user.AuthVersion.ToString())
        };
        if (user.Role == "client") claims.Add(new Claim("plan", plan));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }
}

