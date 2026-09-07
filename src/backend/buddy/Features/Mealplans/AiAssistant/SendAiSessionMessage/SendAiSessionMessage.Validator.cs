using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class SendAiSessionMessageValidator : AbstractValidator<SendAiSessionMessage>
{
    public SendAiSessionMessageValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}
