namespace buddy.Features.Users;

public sealed class PostgresOptions
{
    public const string SectionName = "ConnectionStrings";

    public required string Postgres { get; init; }
}
