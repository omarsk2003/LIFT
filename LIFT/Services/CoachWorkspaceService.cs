using LIFT.Data;

namespace LIFT.Services;

public sealed class CoachWorkspaceService(LiftDbContext db) : SpecialistWorkspaceService(db), ICoachWorkspaceService
{
    protected override string SpecialistRole => "coach";
}
