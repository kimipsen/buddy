# SonarCloud known issues (C# backend)

Read the flagged file yourself before acting on any rule here. For the false positives, the fix is marking the issue Won't Fix / False Positive in SonarCloud (or tuning the quality profile), not changing code.

## False positives - don't flag, don't "fix"

- **`csharpsquid:S3903`, `csharpsquid:S1186`, `csharpsquid:S3060` on any file declaring a `union`.** The analyzer doesn't understand the preview `union` syntax:
  - S3903 ("types should be defined in named namespaces") - parser loses namespace scope after the unrecognized declaration; the types are correctly namespaced.
  - S1186 ("empty methods") - reads `public union Foo(A, B, C)` as an empty method.
  - S3060 ("offload this type test to a subclass") - flags the union's `this switch { A => ..., B => ... }` discriminator, which is the idiomatic pattern. "Fixing" it means removing the union.
- **`csharpsquid:S8970`** (unneeded null-forgiving `!`). Automatic Analysis doesn't run a real `dotnet build` and misses `<Nullable>enable</Nullable>` from `src/backend/Directory.Build.props`. Confirm nullable is enabled project-wide; if so, it's a scanner false positive.
- **`csharpsquid:S107`** (too many parameters, threshold 7) on Wolverine `Handle` methods and minimal-API endpoint lambdas. Command + injected stores/services + `CancellationToken` routinely exceed 7. Don't split signatures to satisfy it.
- **`csharpsquid:S2094`** (empty record) on a no-payload union case, e.g. `Result<T>.NotFound` / `Result<T>.Forbidden` in `src/backend/buddy/Common/Result.cs`. Intentional marker types.

## Real - treat as a bug

- **`csharpsquid:S2201`** ("use the return value") on `events.Reverse()` in event-store code. On `IReadOnlyList<T>`, `.Reverse()` is the non-mutating `Enumerable.Reverse()`; a bare `events.Reverse();` silently does nothing and callers get the wrong order. Use the result: `return [.. events.Reverse().Select(...)];`. This shipped once in `MartenUserEventStore.ReadBackwardAsync`.
