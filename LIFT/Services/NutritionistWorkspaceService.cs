using LIFT.Data;

namespace LIFT.Services;

public sealed class NutritionistWorkspaceService(LiftDbContext db) : SpecialistWorkspaceService(db), INutritionistWorkspaceService
{
    protected override string SpecialistRole => "nutritionist";
}
