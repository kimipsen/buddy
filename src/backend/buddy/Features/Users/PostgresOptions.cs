using System.ComponentModel.DataAnnotations;

namespace buddy.Features.Users;

public sealed class PostgresOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required]
    public required string Postgres { get; init; }
}
