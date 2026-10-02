using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class ReplacePrintTemplateGuardianColorsEndpoint
{
    public static RouteGroupBuilder MapReplacePrintTemplateGuardianColors(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapPut("/{templateId:guid}/colors", async Task<Results<Ok<PrintTemplateResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid templateId,
            ReplacePrintTemplateGuardianColorsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // RespectNullableAnnotations doesn't cover collection elements, so this stays.
            if (request.Colors.Any(c => c is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("colors must not contain null entries.").ToEnvelope(httpContext));
            }

            var colors = request.Colors
                .Select(c => new GuardianColor(new UserId(c.GuardianId), new Color(c.Color.Trim())))
                .ToList();
            var command = ReplacePrintTemplateGuardianColors.FromClaims(principal, new PrintTemplateId(templateId), colors);
            var result = await bus.InvokeAsync<Result<PrintTemplateResponse>>(command, cancellationToken);

            return result switch
            {
                Result<PrintTemplateResponse>.Success(var template) => TypedResults.Ok(template),
                Result<PrintTemplateResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<PrintTemplateResponse>.NotFound => TypedResults.NotFound(),
                // CheckManage never returns Forbidden -- not in this route's declared results.
                Result<PrintTemplateResponse>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ReplacePrintTemplateGuardianColors");

        return printTemplates;
    }
}

public sealed record ReplacePrintTemplateGuardianColorsRequest(IReadOnlyList<GuardianColorRequest> Colors);

public sealed record GuardianColorRequest(Guid GuardianId, string Color);
