using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.PrintTemplates;

public static class CreatePrintTemplateEndpoint
{
    public static RouteGroupBuilder MapCreatePrintTemplate(this RouteGroupBuilder printTemplates)
    {
        printTemplates.MapPost("/", async Task<Results<Ok<PrintTemplateResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            CreatePrintTemplateRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreatePrintTemplate.FromClaims(
                principal,
                request.Name ?? "",
                request.GroupId is { } groupId ? new GroupId(groupId) : null);
            var result = await bus.InvokeAsync<Result<PrintTemplateResponse>>(command, cancellationToken);

            return result switch
            {
                Result<PrintTemplateResponse>.Success(var template) => TypedResults.Ok(template),
                Result<PrintTemplateResponse>.Forbidden => TypedResults.Forbid(),
                Result<PrintTemplateResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<PrintTemplateResponse>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CreatePrintTemplate");

        return printTemplates;
    }
}

public sealed record CreatePrintTemplateRequest(string? Name, Guid? GroupId);
