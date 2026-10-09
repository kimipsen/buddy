using Microsoft.OpenApi;

namespace buddy.Common.OpenApi;

// The generator only marks constructor parameters without a default as required, which is right for
// a request (a client may leave the rest out) but wrong for a response: the server always writes
// every property. So a schema that only ever appears in responses gets every property required, and
// a generated client doesn't have to treat each field as possibly missing. A schema that is also
// sent (PrintTemplateRow, RecurrenceRuleRequest, ...) keeps the request's view.
public static class ResponseSchemaRequirements
{
    public static void Apply(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { Count: > 0 } schemas)
        {
            return;
        }

        var sent = new HashSet<string>();
        var received = new HashSet<string>();

        foreach (var operation in document.Paths?.Values.SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>()) ?? [])
        {
            foreach (var mediaType in operation.RequestBody?.Content?.Values ?? [])
            {
                Collect(mediaType.Schema, schemas, sent);
            }

            foreach (var parameter in operation.Parameters ?? [])
            {
                Collect(parameter.Schema, schemas, sent);
            }

            foreach (var mediaType in operation.Responses?.Values.SelectMany(response => response.Content?.Values ?? []) ?? [])
            {
                Collect(mediaType.Schema, schemas, received);
            }
        }

        foreach (var name in received.Except(sent))
        {
            if (schemas[name] is OpenApiSchema { Properties.Count: > 0 } schema)
            {
                schema.Required ??= new HashSet<string>();
                schema.Required.UnionWith(schema.Properties.Keys);
            }
        }
    }

    // Every component schema reachable from this one, following $refs.
    private static void Collect(IOpenApiSchema? schema, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> found)
    {
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference { Reference.Id: { } id })
        {
            if (found.Add(id) && schemas.TryGetValue(id, out var target))
            {
                Collect(target, schemas, found);
            }

            return;
        }

        IEnumerable<IOpenApiSchema?> children =
        [
            schema.Items,
            schema.AdditionalProperties,
            .. schema.Properties?.Values ?? [],
            .. schema.OneOf ?? [],
            .. schema.AnyOf ?? [],
            .. schema.AllOf ?? [],
        ];

        foreach (var child in children)
        {
            Collect(child, schemas, found);
        }
    }
}
