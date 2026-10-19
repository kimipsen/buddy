using buddy.Common.Validation;

using FluentValidation;

using Xunit;

namespace buddy.IntegrationTests.Common.Validation;

// Every validation problem reaches the client with camelCase JSON-path keys, whether a validator
// or a handler built it (docs/backend/http-status-codes.md, Error Response Shape).
public sealed class ValidationProblemTests
{
    [Fact]
    public void Keys_are_camel_cased_per_path_segment_and_keep_their_indexers()
    {
        var problem = new ValidationProblem(new Dictionary<string, string[]>
        {
            ["Days[0].Week"] = ["a"],
            ["Recurrence.IntervalCount"] = ["b"],
            ["ServedWithin"] = ["c"],
            ["rows[2].childId"] = ["d"],
            [""] = ["e"],
        });

        Assert.Equal(
            ["", "days[0].week", "recurrence.intervalCount", "rows[2].childId", "servedWithin"],
            problem.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["a"], problem.Errors["days[0].week"]);
    }

    [Fact]
    public void Keys_that_only_differ_in_case_are_merged()
    {
        var problem = new ValidationProblem(new Dictionary<string, string[]>
        {
            ["Icon"] = ["blank"],
            ["icon"] = ["malformed"],
        });

        Assert.Equal(["blank", "malformed"], Assert.Single(problem.Errors).Value);
    }

    [Fact]
    public void A_nested_collection_rule_reports_the_request_path()
    {
        var validator = new InlineValidator<Plan>();
        validator.RuleForEach(x => x.Days).ChildRules(day => day.RuleFor(d => d.Week).GreaterThanOrEqualTo(0));

        var problem = ValidationProblem.FromFluentValidation(validator.Validate(new Plan([new Day(0), new Day(-1)])));

        Assert.Equal(["days[1].week"], problem.Errors.Keys);
    }

    public sealed record Plan(IReadOnlyList<Day> Days);

    public sealed record Day(int Week);
}
