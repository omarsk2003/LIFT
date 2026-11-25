using Microsoft.AspNetCore.Authentication;
using LIFT.Data;
using LIFT.Infrastructure;
using LIFT.Models;
using LIFT.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddProvider(new DailyFileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "logs")));
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<LiftDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordHasher<LiftUser>, PasswordHasher<LiftUser>>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAdminCatalogService, AdminCatalogService>();
builder.Services.AddScoped<IAdminSupportService, AdminSupportService>();
builder.Services.AddScoped<IAdminAccountsService, AdminAccountsService>();
builder.Services.AddScoped<IAdminChangesService, AdminChangesService>();
builder.Services.AddScoped<IPublicContactService, PublicContactService>();
builder.Services.AddScoped<ISpecialistRegistrationService, SpecialistRegistrationService>();
builder.Services.AddScoped<INutritionistService, NutritionistService>();
builder.Services.AddScoped<INutritionistWorkspaceService, NutritionistWorkspaceService>();
builder.Services.AddScoped<ICoachWorkspaceService, CoachWorkspaceService>();
builder.Services.AddScoped<ICoachService, CoachService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IClientInteractionService, ClientInteractionService>();
builder.Services.AddScoped<IPaymentCheckoutService, PaymentCheckoutService>();
builder.Services.AddHostedService<SubscriptionExpiryService>();
builder.Services.AddHttpClient<IPayPalGateway, PayPalGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IClientAiService, ClientAiService>();
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434/");
    client.Timeout = TimeSpan.FromMinutes(4);
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "LIFT.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnValidatePrincipal = async context =>
        {
            var path = context.Request.Path.Value ?? "";
            var protectedPage = path.Equals("/Admin", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/Coach", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/Nutritionist", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/Client", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Admin/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Coach/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Nutrition/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Nutritionist/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Client/", StringComparison.OrdinalIgnoreCase);
            if (!protectedPage || (Path.HasExtension(path) && !path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))) return;
            var idText = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var db = context.HttpContext.RequestServices.GetRequiredService<LiftDbContext>();
            var versionText = context.Principal?.FindFirst("auth_version")?.Value;
            var parsedId = Guid.TryParse(idText, out var id);
            var parsedVersion = int.TryParse(versionText, out var cookieVersion);
            var validCookie = parsedId && parsedVersion;
            var currentVersion = validCookie
                ? await db.Users.AsNoTracking()
                    .Where(user => user.Id == id && user.Role == role && user.Status == "active")
                    .Select(user => (int?)user.AuthVersion).SingleOrDefaultAsync()
                : null;
            if (!validCookie || currentVersion != cookieVersion)            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

var app = builder.Build();
if (Environment.GetEnvironmentVariable("LIFT_SEED_ADMIN_DEMO") == "1")
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Admin demo data can only be seeded in Development.");
    await AdminDemoSeeder.RunAsync(app.Services);
    return;
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Keep existing links working while each HTML page is migrated to a Razor view.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var legacyAdminDestination = path.ToLowerInvariant() switch
    {
        "/admin/dashboard/dashboard.html" => "/Admin",
        "/admin/clientmanagement/client-management.html" => "/Admin#accounts",
        "/admin/coachmanagement/coach-management.html" => "/Admin#specialists",
        "/admin/nutritionistmanagement/nutritionist-management.html" => "/Admin#specialists",
        "/admin/admins/admins.html" => "/Admin/AdminAccounts",
        "/admin/messages/messages.html" => "/Admin/Support",
        "/admin/conversation/conversation.html" => "/Admin/Support",
        "/admin/subscriptions/subscriptions.html" => "/Admin/Subscriptions",
        "/admin/homepagemanagement/home-page-management.html" => "/Admin/SiteAppearance",
        _ => null
    };
    if (legacyAdminDestination is not null)
    {
        context.Response.Redirect(legacyAdminDestination);
        return;
    }
    var coachDestination = path.ToLowerInvariant() switch
    {
        "/coach/dashboard/dashboard.html" => "/Coach",
        "/coach/myclients/my-clients.html" => "/Coach/MyClients",
        "/coach/clientdetails/client-details.html" => "/Coach/MyClients",
        "/coach/workoutplan/workout-plan.html" => "/Coach/MyClients",
        "/coach/profile/profile.html" => "/Coach/Profile",
        "/coach/messages/messages.html" => "/Coach/Messages",
        "/coach/adminchat/admin-chat.html" => "/Coach/Messages",
        "/coach/exercisecoaching/exercise-coaching.html" => "/Coach/Guidance",
        "/coach/meetings/meetings.html" => "/Coach/Meetings",
        _ => null
    };
    if (coachDestination is not null) { context.Response.Redirect(coachDestination); return; }
    if (path.Equals("/Coach/Registration/registration.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/CoachRegistration/Apply");
        return;
    }
    var nutritionDestination = path.ToLowerInvariant() switch
    {
        "/nutrition/dashboard/dashboard.html" => "/Nutritionist",
        "/nutrition/myclients/my-clients.html" => "/Nutritionist/MyClients",
        "/nutrition/clientdetails/client-details.html" => "/Nutritionist/MyClients",
        "/nutrition/nutritionplan/nutrition-plan.html" => "/Nutritionist/MyClients",
        "/nutrition/profile/profile.html" => "/Nutritionist/Profile",
        "/nutrition/messages/messages.html" => "/Nutritionist/Messages",
        "/nutrition/adminchat/admin-chat.html" => "/Nutritionist/Messages",
        "/nutrition/nutritioncoaching/nutrition-coaching.html" => "/Nutritionist/Guidance",
        "/nutrition/meetings/meetings.html" => "/Nutritionist/Meetings",
        _ => null
    };
    if (nutritionDestination is not null) { context.Response.Redirect(nutritionDestination); return; }
    var clientDestination = path.ToLowerInvariant() switch
    {
        "/client/dashboard/dashboard.html" => "/Client",
        "/client/profile/profile.html" => "/Client/Profile",
        "/client/personalinformation/personal-information.html" => "/Client/Profile",
        "/client/healthlifestyle/health-lifestyle.html" => "/Client/Profile",
        "/client/workout/workout.html" => "/Client/Workout",
        "/client/nutrition/nutrition.html" => "/Client/Nutrition",
        "/client/progress/progress.html" => "/Client/Progress",
        "/client/messages/messages.html" => "/Client/Messages",
        "/client/support/support.html" => "/Client/Messages",
        "/client/supportconversation/support-conversation.html" => "/Client/Messages",
        "/client/meetings/meetings.html" => "/Client/Meetings",
        "/client/subscription/subscription.html" => "/Client/Subscription",
        "/client/subscriptionselection/subscription-selection.html" => "/Client/Subscription",
        "/client/coaches/coaches.html" => "/Client/Subscription",
        "/client/nutritionists/nutritionists.html" => "/Client/Subscription",
        "/client/specialistdetails/specialist-details.html" => "/Client/Subscription",
        "/client/aiassistant/ai-assistant.html" => "/Client/AiAssistant",
        _ => null
    };
    if (clientDestination is not null) { context.Response.Redirect(clientDestination); return; }
    if (path.Equals("/Nutrition/Registration/registration.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/NutritionistRegistration/Apply");
        return;
    }
    if (path.Equals("/Shared/Login/login.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Account/Login");
        return;
    }
    if (path.Equals("/Client/Registration/registration.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Account/Register");
        return;
    }
    if (path.Equals("/Public/LandingPage/landing-page.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Public/Landing" + context.Request.QueryString);
        return;
    }
    if (path.Equals("/Public/AboutUs/about-us.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Public/About" + context.Request.QueryString);
        return;
    }
    if (path.Equals("/Public/ContactUs/contact-us.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Public/Contact" + context.Request.QueryString);
        return;
    }
    var role = path.StartsWith("/Client/", StringComparison.OrdinalIgnoreCase) ? "client" :
        path.StartsWith("/Coach/", StringComparison.OrdinalIgnoreCase) ? "coach" :
        path.StartsWith("/Nutrition/", StringComparison.OrdinalIgnoreCase) ? "nutritionist" :
        path.StartsWith("/Admin/", StringComparison.OrdinalIgnoreCase) ? "admin" : null;
    var isSpecialistRegistration =
        path.Equals("/Coach/Registration/registration.html", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/Nutrition/Registration/registration.html", StringComparison.OrdinalIgnoreCase);
    if (role is not null && path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
        && !isSpecialistRegistration && !context.User.IsInRole(role))
    {
        context.Response.Redirect("/Account/Login");
        return;
    }
    await next();
});

app.UseStaticFiles();
app.MapStaticAssets();
app.MapControllerRoute(name: "client", pattern: "Client/{action=Index}/{id?}", defaults: new { controller = "Client" }).WithStaticAssets();
app.MapControllerRoute(name: "coach", pattern: "Coach/{action=Index}/{id?}", defaults: new { controller = "Coach" }).WithStaticAssets();
app.MapControllerRoute(name: "nutritionist", pattern: "Nutritionist/{action=Index}/{id?}", defaults: new { controller = "Nutritionist" }).WithStaticAssets();
app.MapControllerRoute(name: "admin", pattern: "Admin/{action=Index}/{id?}", defaults: new { controller = "Admin" }).WithStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Public}/{action=Landing}/{id?}").WithStaticAssets();
app.Run();



