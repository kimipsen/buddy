using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class UpdateMealSlotTimesHandler
{
    public static async Task<Result<Unit>> Handle(
        UpdateMealSlotTimes command,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var now = DateTimeOffset.UtcNow;
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            var newId = MealPlanId.New();

            var events = new List<MealPlanEvent> { new MealPlanCreated(newId, command.ChildId, now) };

            foreach (var (slot, time) in command.Times)
            {
                events.Add(new MealPlanSlotTimeSet(newId, slot, time, userId, now));
            }

            await mealPlans.CreateAsync(newId, events, cancellationToken);

            return new Result<Unit>.Success(Unit.Value);
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
        var plan = MealPlan.Rehydrate(planEvents)!;

        var changes = new List<MealPlanEvent>();

        foreach (var (slot, time) in command.Times)
        {
            if (!plan.SlotTimes.TryGetValue(slot, out var existing) || existing != time)
            {
                changes.Add(new MealPlanSlotTimeSet(mealPlanId, slot, time, userId, now));
            }
        }

        if (changes.Count > 0)
        {
            await mealPlans.AppendAsync(mealPlanId, changes, cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
