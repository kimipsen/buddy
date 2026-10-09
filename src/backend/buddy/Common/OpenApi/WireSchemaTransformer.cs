using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using buddy.Serialization;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace buddy.Common.OpenApi;

// Makes a schema describe what the serializer actually writes for types the generator can't see
// through, because a custom converter decides their shape (it emits {} for those). Each rule is the
// converter's own, so the schema and the wire can't disagree. See
// docs/backend/analysis/openapi-client-contract.md, "schemas describe what goes over the wire".
public sealed class WireSchemaTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        // Enums go by member name, through the JsonStringEnumConverter in Program.cs. When a property
        // holds a nullable enum, the generator also adds null to the shared component's values. The
        // property itself already allows null, so the component lists exactly the names.
        if ((Nullable.GetUnderlyingType(type) ?? type) is { IsEnum: true } enumType)
        {
            schema.Type = JsonSchemaType.String;
            schema.Enum = [.. Enum.GetNames(enumType).Select(name => (JsonNode)JsonValue.Create(name))];
            return;
        }

        // The web defaults let a number be read from a JSON string too, so the generator types every
        // number as integer|string with a digits pattern. Buddy always writes numbers, and its
        // clients send numbers, so the contract says number.
        if (schema.Type is { } numeric
            && numeric.HasFlag(JsonSchemaType.String)
            && (numeric.HasFlag(JsonSchemaType.Integer) || numeric.HasFlag(JsonSchemaType.Number)))
        {
            schema.Type = numeric & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        // A computed, get-only property (CalendarItemOccurrence.SortAt, PreviewMealPlanImportRequest.FormatOrAuto)
        // is only ever written, never read from a request body.
        if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object && schema.Properties is { Count: > 0 } properties)
        {
            foreach (var property in context.JsonTypeInfo.Properties.Where(p => p.Set is null && p.AssociatedParameter is null))
            {
                if (properties.TryGetValue(property.Name, out var propertySchema) && propertySchema is OpenApiSchema writable)
                {
                    writable.ReadOnly = true;
                }
            }
        }

        // UserId, GroupId, Color, Language, ...: StronglyTypedIdJsonConverterFactory writes the bare value.
        // A nullable property (UserId? ChildId) keeps its null; RespectNullableAnnotations is on.
        if (StronglyTypedIdJsonConverterFactory.TryGetValueType(type, out var valueType))
        {
            var valueSchema = await context.GetOrCreateSchemaAsync(valueType, null, cancellationToken);
            schema.Type = context.JsonPropertyInfo is { IsGetNullable: true }
                ? valueSchema.Type | JsonSchemaType.Null
                : valueSchema.Type;
            schema.Format = valueSchema.Format;
            return;
        }

        // A list of wrappers (IReadOnlyList<CalendarId>): the generator drops the empty item schema.
        if (context.JsonTypeInfo.ElementType is { } elementType
            && StronglyTypedIdJsonConverterFactory.TryGetValueType(elementType, out var elementValueType)
            && schema.Items is null or OpenApiSchema { Type: null, OneOf: null })
        {
            var elementSchema = await context.GetOrCreateSchemaAsync(elementValueType, null, cancellationToken);
            schema.Items = new OpenApiSchema { Type = elementSchema.Type, Format = elementSchema.Format };
        }

        // ItemScheduleRequest, PickupAssigneeDto, ...: an object with a numeric "kind" plus that
        // case's own fields (KindDiscriminatedJsonConverter).
        if (type.GetCustomAttribute<JsonConverterAttribute>(inherit: false)?.ConverterType is { } converterType
            && typeof(IKindDiscriminatedConverter).IsAssignableFrom(converterType)
            && Activator.CreateInstance(converterType) is IKindDiscriminatedConverter converter)
        {
            schema.OneOf = [];

            foreach (var (kind, caseType) in converter.Cases.OrderBy(pair => pair.Key))
            {
                var caseSchema = await context.GetOrCreateSchemaAsync(caseType, null, cancellationToken);
                caseSchema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                caseSchema.Properties[IKindDiscriminatedConverter.KindProperty] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Integer,
                    Format = "int32",
                    Enum = [JsonValue.Create(kind)],
                    Description = caseType.Name,
                };
                caseSchema.Required ??= new HashSet<string>();
                caseSchema.Required.Add(IKindDiscriminatedConverter.KindProperty);
                schema.OneOf.Add(caseSchema);
            }

            schema.Description = $"One of {converter.Cases.Count} cases, picked by the numeric \"{IKindDiscriminatedConverter.KindProperty}\": "
                + string.Join(", ", converter.Cases.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key} = {pair.Value.Name}"))
                + ".";
        }
    }

    // A nullable wrapper property (MealPlanImportId? ImportId) can reach this transformer with the
    // shared component itself, so the component picks up null. Every nullable use of a component is
    // already a oneOf [null, $ref], so a scalar component never needs it. Run as a document step.
    public static void RemoveNullFromScalarComponents(OpenApiDocument document)
    {
        foreach (var schema in document.Components?.Schemas?.Values.OfType<OpenApiSchema>() ?? [])
        {
            if (schema.Type is { } type && type != JsonSchemaType.Null && type.HasFlag(JsonSchemaType.Null) && schema.Properties is null or { Count: 0 })
            {
                schema.Type = type & ~JsonSchemaType.Null;
            }
        }
    }
}
