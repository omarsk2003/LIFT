using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Data;

public sealed class LiftDbContext(DbContextOptions<LiftDbContext> options) : DbContext(options)
{
    public DbSet<LiftUser> Users => Set<LiftUser>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<LiftPayment> Payments => Set<LiftPayment>();
    public DbSet<LiftPaymentEvent> PaymentEvents => Set<LiftPaymentEvent>();
    public DbSet<SubscriptionSpecialistSelection> SubscriptionSpecialistSelections => Set<SubscriptionSpecialistSelection>();
    public DbSet<SpecialistProfile> SpecialistProfiles => Set<SpecialistProfile>();
    public DbSet<SpecialistCertificate> SpecialistCertificates => Set<SpecialistCertificate>();
    public DbSet<LiftNotification> Notifications => Set<LiftNotification>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<PromoCodePlan> PromoCodePlans => Set<PromoCodePlan>();
    public DbSet<LiftConversation> Conversations => Set<LiftConversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<LiftMessage> Messages => Set<LiftMessage>();
    public DbSet<SpecialistAssignment> SpecialistAssignments => Set<SpecialistAssignment>();
    public DbSet<SpecialistChangeRequest> SpecialistChangeRequests => Set<SpecialistChangeRequest>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<NutritionPlanMeal> NutritionPlanMeals => Set<NutritionPlanMeal>();
    public DbSet<WorkoutPlanItem> WorkoutPlanItems => Set<WorkoutPlanItem>();
    public DbSet<ClientTrainingDay> ClientTrainingDays => Set<ClientTrainingDay>();
    public DbSet<ClientMeasurement> Measurements => Set<ClientMeasurement>();
    public DbSet<ClientActivityLog> ActivityLogs => Set<ClientActivityLog>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiAnalysisRun> AiAnalysisRuns => Set<AiAnalysisRun>();
    public DbSet<AiPlanGeneration> AiPlanGenerations => Set<AiPlanGeneration>();
    public DbSet<SpecialistGuidanceNote> SpecialistGuidanceNotes => Set<SpecialistGuidanceNote>();
    public DbSet<LiftMeeting> Meetings => Set<LiftMeeting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiftUser>(entity =>
        {
            entity.ToTable("Users", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        });
        modelBuilder.Entity<ClientProfile>(entity =>
        {
            entity.ToTable("ClientProfiles", "dbo");
            entity.HasKey(x => x.UserId);
            entity.HasOne<LiftUser>().WithOne().HasForeignKey<ClientProfile>(x => x.UserId);
            entity.Property(x => x.HeightCm).HasPrecision(5, 2);
            entity.Property(x => x.StartingWeightKg).HasPrecision(5, 2);
            entity.Property(x => x.SleepHours).HasPrecision(3, 1);
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        });
        modelBuilder.Entity<ClientTrainingDay>(entity =>
        {
            entity.ToTable("ClientTrainingDays", "dbo");
            entity.HasKey(x => new { x.ClientId, x.DayOfWeek });
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
        });
        modelBuilder.Entity<ClientMeasurement>(entity =>
        {
            entity.ToTable("Measurements", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WeightKg).HasPrecision(5, 2);
            entity.Property(x => x.BodyFatPercent).HasPrecision(5, 2);
            entity.Property(x => x.MusclePercent).HasPrecision(5, 2);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
        });
        modelBuilder.Entity<ClientActivityLog>(entity =>
        {
            entity.ToTable("ActivityLogs", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActivityType).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Label).HasMaxLength(200);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
        });
        modelBuilder.Entity<AiModel>(entity =>
        {
            entity.ToTable("AiModels", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.ModelKey).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(20).IsUnicode(false);
        });
        modelBuilder.Entity<AiConversation>(entity =>
        {
            entity.ToTable("AiConversations", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
        });
        modelBuilder.Entity<AiMessage>(entity =>
        {
            entity.ToTable("AiMessages", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ModelName).HasMaxLength(100);
            entity.HasOne<AiConversation>().WithMany().HasForeignKey(x => x.ConversationId);
            entity.HasOne<AiModel>().WithMany().HasForeignKey(x => x.AiModelId);
        });
        modelBuilder.Entity<AiAnalysisRun>(entity =>
        {
            entity.ToTable("AiAnalysisRuns", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.AnalysisType).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ErrorCode).HasMaxLength(100);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
            entity.HasOne<AiModel>().WithMany().HasForeignKey(x => x.AiModelId);
        });
        modelBuilder.Entity<AiPlanGeneration>(entity =>
        {
            entity.ToTable("AiPlanGenerations", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanType).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ModelName).HasMaxLength(100);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
            entity.HasOne<AiConversation>().WithMany().HasForeignKey(x => x.ConversationId);
            entity.HasOne<PlanVersion>().WithMany().HasForeignKey(x => x.PlanVersionId);
            entity.HasOne<AiModel>().WithMany().HasForeignKey(x => x.AiModelId);
        });
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.MonthlyPrice).HasPrecision(10, 2);
        });
        modelBuilder.Entity<SpecialistProfile>(entity =>
        {
            entity.ToTable("SpecialistProfiles", "dbo");
            entity.HasKey(x => x.UserId);
            entity.HasOne<LiftUser>().WithOne().HasForeignKey<SpecialistProfile>(x => x.UserId);
            entity.Property(x => x.Specialty).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Bio).HasMaxLength(1000);
        });
        modelBuilder.Entity<SpecialistCertificate>(entity =>
        {
            entity.ToTable("SpecialistCertificates", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasOne<SpecialistProfile>().WithMany().HasForeignKey(x => x.SpecialistId);
        });
        modelBuilder.Entity<LiftNotification>(entity =>
        {
            entity.ToTable("Notifications", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasOne<LiftUser>().WithMany().HasForeignKey(x => x.UserId);
            entity.Property(x => x.Type).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(1000);
            entity.Property(x => x.TargetUrl).HasMaxLength(500);
        });
        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.ToTable("PromoCodes", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        });
        modelBuilder.Entity<PromoCodePlan>(entity =>
        {
            entity.ToTable("PromoCodePlans", "dbo");
            entity.HasKey(x => new { x.PromoCodeId, x.PlanId });
            entity.HasOne<PromoCode>().WithMany().HasForeignKey(x => x.PromoCodeId);
            entity.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId);
        });
        modelBuilder.Entity<LiftConversation>(entity =>
        {
            entity.ToTable("Conversations", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Subject).HasMaxLength(200);
        });
        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.ToTable("ConversationParticipants", "dbo");
            entity.HasKey(x => new { x.ConversationId, x.UserId });
            entity.HasOne<LiftConversation>().WithMany().HasForeignKey(x => x.ConversationId);
            entity.HasOne<LiftUser>().WithMany().HasForeignKey(x => x.UserId);
        });
        modelBuilder.Entity<LiftMessage>(entity =>
        {
            entity.ToTable("Messages", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasOne<LiftConversation>().WithMany().HasForeignKey(x => x.ConversationId);
            entity.HasOne<ConversationParticipant>().WithMany()
                .HasForeignKey(x => new { x.ConversationId, x.SenderId })
                .HasPrincipalKey(x => new { x.ConversationId, x.UserId });
        });
        modelBuilder.Entity<SpecialistAssignment>(entity =>
        {
            entity.ToTable("SpecialistAssignments", "dbo", table => table.UseSqlOutputClause(false));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SpecialistRole).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
            entity.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId);
            entity.HasOne<SpecialistProfile>().WithMany().HasForeignKey(x => x.SpecialistId);
        });
        modelBuilder.Entity<SpecialistChangeRequest>(entity =>
        {
            entity.ToTable("SpecialistChangeRequests", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.AdminNote).HasMaxLength(1000);
            entity.HasOne<SpecialistAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SpecialistAssignment>().WithMany().HasForeignKey(x => x.ReplacementAssignmentId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<LiftUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.ToTable("ContactMessages", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(180);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
        });
        modelBuilder.Entity<SpecialistGuidanceNote>(entity =>
        {
            entity.ToTable("SpecialistGuidanceNotes", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Topic).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.HasOne<SpecialistProfile>().WithMany().HasForeignKey(x => x.SpecialistId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<LiftMeeting>(entity =>
        {
            entity.ToTable("Meetings", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Topic).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.MeetingUrl).HasMaxLength(1000);
            entity.HasOne<SpecialistProfile>().WithMany().HasForeignKey(x => x.SpecialistId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<WorkoutPlanItem>(entity =>
        {
            entity.ToTable("WorkoutPlanItems", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExerciseName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Reps).HasMaxLength(50);
            entity.Property(x => x.Instructions).HasMaxLength(1000);
            entity.HasOne<PlanVersion>().WithMany().HasForeignKey(x => x.PlanVersionId);
        });
        modelBuilder.Entity<PlanVersion>(entity =>
        {
            entity.ToTable("PlanVersions", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanType).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Source).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.ProteinGrams).HasPrecision(7, 2);
            entity.Property(x => x.CarbsGrams).HasPrecision(7, 2);
            entity.Property(x => x.FatGrams).HasPrecision(7, 2);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
            entity.HasOne<LiftUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<LiftUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => new { x.ClientId, x.PlanType, x.VersionNumber }).IsUnique();
        });
        modelBuilder.Entity<NutritionPlanMeal>(entity =>
        {
            entity.ToTable("NutritionPlanMeals", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MealName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasOne<PlanVersion>().WithMany().HasForeignKey(x => x.PlanVersionId);
        });        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PriceAtStart).HasPrecision(10, 2);
            entity.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ClientId);
            entity.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId);
        });
        modelBuilder.Entity<LiftPayment>(entity =>
        {
            entity.ToTable("Payments", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.ProviderPaymentId).HasMaxLength(200);
            entity.Property(x => x.Amount).HasPrecision(10, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId);
        });
        modelBuilder.Entity<LiftPaymentEvent>(entity =>
        {
            entity.ToTable("PaymentEvents", "dbo"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.ProviderEventId).HasMaxLength(200);
            entity.Property(x => x.EventType).HasMaxLength(100);
            entity.Property(x => x.ProcessingStatus).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ErrorCode).HasMaxLength(100);
            entity.HasOne<LiftPayment>().WithMany().HasForeignKey(x => x.PaymentId);
        });
        modelBuilder.Entity<SubscriptionSpecialistSelection>(entity =>
        {
            entity.ToTable("SubscriptionSpecialistSelections", "dbo");
            entity.HasKey(x => new { x.SubscriptionId, x.SpecialistRole });
            entity.Property(x => x.SpecialistRole).HasMaxLength(20).IsUnicode(false);
            entity.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId);
            entity.HasOne<SpecialistProfile>().WithMany().HasForeignKey(x => x.SpecialistId);
        });
    }
}

