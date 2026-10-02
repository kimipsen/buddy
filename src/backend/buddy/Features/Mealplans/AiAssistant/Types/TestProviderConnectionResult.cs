using System.Text.Json.Serialization;

namespace buddy.Features.Mealplans;

// Failed is a normal, expected outcome here (a bad/expired key, no quota, ...) -- distinct from the
// command itself failing validation/authorization, which still goes through the usual Result<T>
// NotFound/Forbidden/Validation cases. Wire shape { "kind": 0 } | { "kind": 1, "message" }.
//
// [JsonPolymorphic] rather than KindDiscriminatedJsonConverter: this is a top-level, response-only
// type, and minimal APIs serialize a top-level value by its runtime type unless the declared type
// is polymorphic, which would skip a converter on the base. The missing-kind 500 that converter
// exists for only affects reading, which never happens here.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Succeeded), 0)]
[JsonDerivedType(typeof(Failed), 1)]
public abstract record TestProviderConnectionResult
{
    public sealed record Succeeded : TestProviderConnectionResult;

    public sealed record Failed(string Message) : TestProviderConnectionResult;
}
