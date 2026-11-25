using System.Data;
using System.Text.Json;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class ClientAiService(LiftDbContext db, IOllamaClient ollama,
    ILogger<ClientAiService> logger) : IClientAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<(bool Paid, ClientProfile Profile)> Access(Guid id, CancellationToken ct)
    {
        var paid = await (from sub in db.Subscriptions.AsNoTracking()
            join plan in db.SubscriptionPlans.AsNoTracking() on sub.PlanId equals plan.Id
            where sub.ClientId == id && sub.Status == "active" && plan.Code != "free"
            select sub.Id).AnyAsync(ct);
        var profile = await db.ClientProfiles.AsNoTracking().SingleAsync(x => x.UserId == id, ct);
        return (paid, profile);
    }

    private static bool Ready(ClientProfile profile) => profile.ProfileCompletedAt != null &&
        !string.IsNullOrWhiteSpace(profile.Goal) && !string.IsNullOrWhiteSpace(profile.FitnessLevel) &&
        !string.IsNullOrWhiteSpace(profile.ActivityLevel);

    private Task<AiModel?> Model(string purpose, CancellationToken ct) => db.AiModels.AsNoTracking()
        .Where(x => x.Purpose == purpose && x.Provider == "ollama" && x.IsActive)
        .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);

    private static string Context(ClientProfile p) =>
        $"Goal: {p.Goal}; fitness level: {p.FitnessLevel}; activity: {p.ActivityLevel}; " +
        $"training location: {p.TrainingLocation}; available minutes: {p.SessionMinutes}; " +
        $"health considerations: {p.HealthNotes}; food habits: {p.FoodHabits}; " +
        $"dietary preferences: {p.DietaryPreferences}.";

    public async Task<ClientAiVm> GetAsync(Guid id, CancellationToken ct)
    {
        var access = await Access(id, ct);
        var conversation = await db.AiConversations.AsNoTracking().Where(x => x.ClientId == id)
            .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var messages = conversation.HasValue ? await db.AiMessages.AsNoTracking()
            .Where(x => x.ConversationId == conversation.Value && x.Role != "system")
            .OrderByDescending(x => x.CreatedAt).Take(40)
            .Select(x => new ClientAiMessageVm(x.Role, x.Body, x.CreatedAt)).ToListAsync(ct) : [];
        messages.Reverse();
        return new ClientAiVm { Entitled = access.Paid, ProfileReady = Ready(access.Profile),
            ConversationId = conversation, Messages = messages,
            Analyses = await db.AiAnalysisRuns.AsNoTracking().Where(x => x.ClientId == id)
                .OrderByDescending(x => x.RequestedAt).Take(10).ToListAsync(ct),
            Generations = await db.AiPlanGenerations.AsNoTracking().Where(x => x.ClientId == id)
                .OrderByDescending(x => x.RequestedAt).Take(10).ToListAsync(ct) };
    }

    public async Task<ClientAiResult> ChatAsync(Guid id, ClientAiChatForm form, CancellationToken ct)
    {
        var access = await Access(id, ct);
        if (!access.Paid) return new(false, "AI chat requires an active paid plan.");
        if (!Ready(access.Profile)) return new(false, "Complete your profile before using AI chat.");
        var model = await Model("chat", ct);
        if (model is null) return new(false, "No chat model is active.");
        var conversation = form.ConversationId.HasValue
            ? await db.AiConversations.SingleOrDefaultAsync(x => x.Id == form.ConversationId && x.ClientId == id, ct)
            : null;
        if (form.ConversationId.HasValue && conversation is null) return new(false, "Conversation not found.");
        if (conversation is null)
        {
            conversation = new AiConversation { Id = Guid.NewGuid(), ClientId = id,
                Title = form.Message.Trim()[..Math.Min(form.Message.Trim().Length, 80)], CreatedAt = DateTimeOffset.UtcNow };
            db.AiConversations.Add(conversation);
        }
        db.AiMessages.Add(new AiMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id,
            Role = "user", Body = form.Message.Trim(), CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        var history = await db.AiMessages.AsNoTracking().Where(x => x.ConversationId == conversation.Id)
            .OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct);
        history.Reverse();
        var messages = new List<OllamaChatMessage> { new("system",
            "You are LIFT's fitness and nutrition assistant. Respond clearly and briefly in the user's language. " +
            "Use the client's context, avoid diagnosis or dangerous exercise/diet advice, and suggest professional review when needed. " + Context(access.Profile)) };
        messages.AddRange(history.Select(x => new OllamaChatMessage(x.Role, x.Body)));
        try
        {
            var answer = await ollama.ChatAsync(model.ModelKey, messages, null, 650, ct);
            db.AiMessages.Add(new AiMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id,
                Role = "assistant", Body = answer[..Math.Min(answer.Length, 6000)],
                ModelName = model.ModelKey, AiModelId = model.Id, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);
            return new(true, null, conversation.Id);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "AI chat failed for client {ClientId} using model {Model}", id, model.ModelKey);
            return new(false, "Local AI is unavailable right now. Your message was saved; try again later.", conversation.Id);
        }
    }

    public async Task<ClientAiResult> AnalyzeAsync(Guid id, CancellationToken ct)
    {
        var access = await Access(id, ct);
        if (!access.Paid) return new(false, "AI analysis requires an active paid plan.");
        if (!Ready(access.Profile)) return new(false, "Complete your profile before requesting analysis.");
        var model = await Model("analysis", ct);
        if (model is null) return new(false, "No analysis model is active.");
        var run = new AiAnalysisRun { Id = Guid.NewGuid(), ClientId = id, AiModelId = model.Id,
            AnalysisType = "lifestyle", Status = "pending", RequestedAt = DateTimeOffset.UtcNow };
        db.AiAnalysisRuns.Add(run); await db.SaveChangesAsync(ct);
        var schema = new { type = "object", properties = new {
            summary = new { type = "string" }, cautions = new { type = "array", items = new { type = "string" } },
            questions = new { type = "array", items = new { type = "string" } } },
            required = new[] { "summary", "cautions", "questions" } };
        try
        {
            var answer = await ollama.ChatAsync(model.ModelKey, [new("system",
                "Analyze this client's fitness and nutrition context. Return concise JSON only. Do not diagnose or prescribe treatment."),
                new("user", Context(access.Profile))], schema, 900, ct);
            using var parsed = JsonDocument.Parse(answer);
            var summary = parsed.RootElement.GetProperty("summary").GetString();
            if (string.IsNullOrWhiteSpace(summary) || summary.Length > 4000) throw new InvalidDataException("Invalid analysis summary.");
            var cautions = parsed.RootElement.GetProperty("cautions").EnumerateArray()
                .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(5);
            var questions = parsed.RootElement.GetProperty("questions").EnumerateArray()
                .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(5);
            run.Summary = summary + "\n\nCautions:\n" + string.Join("\n", cautions) +
                "\n\nQuestions to clarify:\n" + string.Join("\n", questions);
            run.Status = "completed"; run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct); return new(true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
        {
            run.Status = "failed"; run.ErrorCode = "ollama_or_output_error"; run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "AI analysis failed for client {ClientId} using model {Model}", id, model.ModelKey);
            return new(false, "Analysis could not be completed. Please try again later.");
        }
    }

    public Task<ClientAiResult> GeneratePlanAsync(Guid id, ClientAiPlanForm form, CancellationToken ct)
        => GenerateDraftAsync(id, form.PlanType, ct);

    private async Task<ClientAiResult> GenerateDraftAsync(Guid id, string type, CancellationToken ct)
    {
        var access = await Access(id, ct);
        if (!access.Paid) return new(false, "AI plans require an active paid plan.");
        if (!Ready(access.Profile)) return new(false, "Complete your profile before generating a plan.");
        var role = type == "workout" ? "coach" : "nutritionist";
        var assigned = await db.SpecialistAssignments.AnyAsync(x => x.ClientId == id && x.SpecialistRole == role &&
            x.Status == "active" && db.Subscriptions.Any(s => s.Id == x.SubscriptionId && s.Status == "active"));
        if (!assigned) return new(false, $"A {role} must be assigned to review this draft.");
        var purpose = type == "workout" ? "workout_plan" : "nutrition_plan";
        var model = await Model(purpose, ct);
        if (model is null) return new(false, "No plan model is active.");
        var run = new AiPlanGeneration { Id = Guid.NewGuid(), ClientId = id, PlanType = type,
            Status = "pending", ModelName = model.ModelKey, AiModelId = model.Id, RequestedAt = DateTimeOffset.UtcNow };
        db.AiPlanGenerations.Add(run); await db.SaveChangesAsync(ct);
        try
        {
            if (type == "workout") await GenerateWorkout(id, access.Profile, model, run, ct);
            else await GenerateNutrition(id, access.Profile, model, run, ct);
            return new(true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException or NullReferenceException)
        {
            run.Status = "failed"; run.ErrorMessage = "Generation or validation failed"; run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "AI {Type} plan failed for client {ClientId} using model {Model}", type, id, model.ModelKey);
            return new(false, "The AI draft could not be validated. Please try again.");
        }
    }

    private async Task GenerateWorkout(Guid id, ClientProfile profile, AiModel model, AiPlanGeneration run, CancellationToken ct)
    {
        var schema = new { type = "object", properties = new {
            title = new { type = "string" }, notes = new { type = "string" },
            exercises = new { type = "array", items = new { type = "object", properties = new {
                day = new { type = "integer" }, name = new { type = "string" }, sets = new { type = "integer" },
                reps = new { type = "string" }, durationMinutes = new { type = "integer" },
                instructions = new { type = "string" } },
                required = new[] { "day", "name", "sets", "reps", "durationMinutes", "instructions" } } } },
            required = new[] { "title", "notes", "exercises" } };
        var answer = await ollama.ChatAsync(model.ModelKey, [new("system",
            "Create a conservative beginner-friendly workout draft for professional coach review. Return JSON only. " +
            "Use days 1-7 and 1-8 exercises. No diagnosis or high-risk training."),
            new("user", Context(profile))], schema, 1100, ct);
        var data = JsonSerializer.Deserialize<WorkoutDraft>(answer, JsonOptions) ?? throw new InvalidDataException();
        if (data.Title.Length is < 1 or > 200 || data.Notes.Length > 2000 || data.Exercises.Count is < 1 or > 8 ||
            data.Exercises.Any(x => x.Day is < 1 or > 7 || x.Name.Length is < 1 or > 160 ||
                x.Sets is < 0 or > 30 || x.Reps.Length > 50 || x.DurationMinutes is < 0 or > 180 || x.Instructions.Length > 1000))
            throw new InvalidDataException("Workout draft outside supported ranges.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var version = (await db.PlanVersions.Where(x => x.ClientId == id && x.PlanType == "workout")
            .MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
        var plan = new PlanVersion { Id = Guid.NewGuid(), ClientId = id, PlanType = "workout",
            VersionNumber = version, Source = "ai", Status = "pending_review",
            Title = data.Title.Trim(), Notes = data.Notes.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        db.PlanVersions.Add(plan);
        short order = 0;
        foreach (var x in data.Exercises) db.WorkoutPlanItems.Add(new WorkoutPlanItem { Id = Guid.NewGuid(),
            PlanVersionId = plan.Id, DayOfWeek = (byte)x.Day, SortOrder = ++order,
            ExerciseName = x.Name.Trim(), Sets = x.Sets == 0 ? null : (short)x.Sets,
            Reps = x.Reps.Trim(), DurationMinutes = x.DurationMinutes == 0 ? null : (short)x.DurationMinutes,
            Instructions = x.Instructions.Trim() });
        run.PlanVersionId = plan.Id; run.Status = "completed"; run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task GenerateNutrition(Guid id, ClientProfile profile, AiModel model, AiPlanGeneration run, CancellationToken ct)
    {
        var schema = new { type = "object", properties = new {
            title = new { type = "string" }, notes = new { type = "string" },
            calories = new { type = "integer" }, proteinGrams = new { type = "number" },
            carbsGrams = new { type = "number" }, fatGrams = new { type = "number" },
            meals = new { type = "array", items = new { type = "object", properties = new {
                name = new { type = "string" }, description = new { type = "string" } },
                required = new[] { "name", "description" } } } },
            required = new[] { "title", "notes", "calories", "proteinGrams", "carbsGrams", "fatGrams", "meals" } };
        var answer = await ollama.ChatAsync(model.ModelKey, [new("system",
            "Create a balanced nutrition draft for professional nutritionist review. Return JSON only. " +
            "Include 1-6 practical meals. Avoid extreme calorie restriction and medical claims."),
            new("user", Context(profile))], schema, 1100, ct);
        var data = JsonSerializer.Deserialize<NutritionDraft>(answer, JsonOptions) ?? throw new InvalidDataException();
        if (data.Title.Length is < 1 or > 200 || data.Notes.Length > 2000 || data.Calories is < 1200 or > 5000 ||
            data.ProteinGrams is < 0 or > 500 || data.CarbsGrams is < 0 or > 800 || data.FatGrams is < 0 or > 300 ||
            data.Meals.Count is < 1 or > 6 || data.Meals.Any(x => x.Name.Length is < 1 or > 100 || x.Description.Length > 1000))
            throw new InvalidDataException("Nutrition draft outside supported ranges.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var version = (await db.PlanVersions.Where(x => x.ClientId == id && x.PlanType == "nutrition")
            .MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
        var plan = new PlanVersion { Id = Guid.NewGuid(), ClientId = id, PlanType = "nutrition",
            VersionNumber = version, Source = "ai", Status = "pending_review",
            Title = data.Title.Trim(), Notes = data.Notes.Trim(), Calories = data.Calories,
            ProteinGrams = data.ProteinGrams, CarbsGrams = data.CarbsGrams, FatGrams = data.FatGrams,
            CreatedAt = DateTimeOffset.UtcNow };
        db.PlanVersions.Add(plan);
        short order = 0;
        foreach (var x in data.Meals) db.NutritionPlanMeals.Add(new NutritionPlanMeal { Id = Guid.NewGuid(),
            PlanVersionId = plan.Id, SortOrder = ++order, MealName = x.Name.Trim(), Description = x.Description.Trim() });
        run.PlanVersionId = plan.Id; run.Status = "completed"; run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private sealed class WorkoutDraft
    {
        public string Title { get; set; } = "";
        public string Notes { get; set; } = "";
        public List<WorkoutDraftItem> Exercises { get; set; } = [];
    }
    private sealed class WorkoutDraftItem
    {
        public int Day { get; set; }
        public string Name { get; set; } = "";
        public int Sets { get; set; }
        public string Reps { get; set; } = "";
        public int DurationMinutes { get; set; }
        public string Instructions { get; set; } = "";
    }
    private sealed class NutritionDraft
    {
        public string Title { get; set; } = "";
        public string Notes { get; set; } = "";
        public int Calories { get; set; }
        public decimal ProteinGrams { get; set; }
        public decimal CarbsGrams { get; set; }
        public decimal FatGrams { get; set; }
        public List<NutritionDraftMeal> Meals { get; set; } = [];
    }
    private sealed class NutritionDraftMeal
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
