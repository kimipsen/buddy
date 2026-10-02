namespace buddy.Features.PrintTemplates;

public sealed record PrintTemplateId(Guid Value)
{
    public static PrintTemplateId New() => new(Guid.CreateVersion7());
}
