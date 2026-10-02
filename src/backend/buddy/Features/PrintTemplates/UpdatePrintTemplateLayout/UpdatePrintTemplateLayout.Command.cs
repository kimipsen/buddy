using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record UpdatePrintTemplateLayout(
    UserId? UserId,
    PrintTemplateId TemplateId,
    PaperSize PaperSize,
    DayOfWeek DefaultStartWeekday,
    bool ShowWeekNumber)
{
    public static UpdatePrintTemplateLayout FromClaims(
        ClaimsPrincipal principal, PrintTemplateId templateId, PaperSize paperSize, DayOfWeek defaultStartWeekday, bool showWeekNumber) =>
        new(principal.GetUserId(), templateId, paperSize, defaultStartWeekday, showWeekNumber);
}
