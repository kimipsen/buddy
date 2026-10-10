using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class AcknowledgeRuleEndpoint
{
    public static RouteGroupBuilder MapAcknowledgeRule(this RouteGroupBuilder houseRules)
    {
        houseRules.MapPut("/children/{childId:guid}/rules/{ruleId:guid}/acknowledgement", (ClaimsPrincipal principal, Guid childId, Guid ruleId, AcknowledgeRuleRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(AcknowledgeRule.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), new RuleId(ruleId), request), bus, httpContext, cancellationToken))
            .ProducesErrorCode(StatusCodes.Status409Conflict, AcknowledgeRule.RevisionChangedCode)
            .WithName("AcknowledgeRule");

        houseRules.MapPut("/groups/{groupId:guid}/rules/{ruleId:guid}/acknowledgement", (ClaimsPrincipal principal, Guid groupId, Guid ruleId, AcknowledgeRuleRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(AcknowledgeRule.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), new RuleId(ruleId), request), bus, httpContext, cancellationToken))
            .ProducesErrorCode(StatusCodes.Status409Conflict, AcknowledgeRule.RevisionChangedCode)
            .WithName("AcknowledgeRuleForGroup");

        return houseRules;
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> HandleAsync(
        AcknowledgeRule command,
        IMessageBus bus,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var outcome = await bus.InvokeAsync<AcknowledgeRuleOutcome>(command, cancellationToken);

        return outcome switch
        {
            AcknowledgeRuleOutcome.Success => TypedResults.NoContent(),
            AcknowledgeRuleOutcome.NotFound => TypedResults.NotFound(),
            AcknowledgeRuleOutcome.Forbidden => TypedResults.Forbid(),
            AcknowledgeRuleOutcome.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            AcknowledgeRuleOutcome.RevisionChanged(var current) => TypedResults.Conflict(new ErrorEnvelope(
                AcknowledgeRule.RevisionChangedCode,
                $"The rule has changed since it was read (now revision {current}). Reload it and read the new text first.",
                new Dictionary<string, string[]>(),
                httpContext.TraceIdentifier)),
        };
    }
}

// ChildId only when a guardian acknowledges on a child's behalf.
public sealed record AcknowledgeRuleRequest(int Revision, Guid? ChildId = null);
