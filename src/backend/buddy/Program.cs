using buddy.Common.Concurrency;
using buddy.Common.Idempotency;
using buddy.Common.Validation;
using buddy.Email;
using buddy.Features.Babysitters;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Mealplans;
using buddy.Features.Medicines;
using buddy.Features.Pickups;
using buddy.Features.PrintTemplates;
using buddy.Features.WorkLocations;
using buddy.Features.Progress;
using buddy.Features.SleepDiaries;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;
using buddy.Serialization;

using FluentValidation;

using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(opts =>
{
    // IKeycloakAdminClient is registered via AddHttpClient<TClient, TImpl>(), which wires it up
    // through HttpClientFactory's internal "opaque" lambda factory -- Wolverine can't inline that
    // into generated constructor code, so it needs the explicit service-location opt-in below
    // (the rest of each handler's dependencies still get the faster constructor-inlined codegen).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<IKeycloakAdminClient>();

    // Same reasoning as above: IAiProviderRegistry ultimately holds a typed HttpClient
    // (AnthropicChatClient), which Wolverine's constructor-codegen can't inline either.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<IAiProviderRegistry>();

    // Optimistic concurrency: every handler invocation tracks the stream versions its store
    // reads saw, so the matching appends are expected-version appends (see StreamVersionTracker).
    opts.Policies.AddMiddleware(typeof(StreamVersionScopeMiddleware));
});

// Add services to the container.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new StronglyTypedIdJsonConverterFactory());
    options.SerializerOptions.Converters.Add(new ValueTupleJsonConverterFactory());

    // Honest request DTOs: a request record's non-nullable member can't arrive as null, and a
    // constructor parameter without a default must be present in the body. Optional fields are
    // nullable with `= null`. HTTP only -- each Marten store keeps its own serializer options.
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});

// Without this, minimal APIs answer an unreadable body with a bare 400 outside Development.
// Throwing lets RequestBindingFailureMiddleware render it as the same ErrorEnvelope a validator
// failure gets.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

var frontendOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(frontendOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.ShouldInclude = api => api.GroupName is null;
});
builder.Services.AddHealthChecks();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddIdempotencyFeature(builder.Configuration);
builder.Services.AddEmail(builder.Configuration);
builder.Services.AddUsersFeature(builder.Configuration);
builder.Services.AddGuardiansFeature(builder.Configuration);
builder.Services.AddGroupsFeature(builder.Configuration);
// TaskLibrary has no dependency on Calendars yet (this is only step 1 of the Task Library plan),
// but is registered before it in anticipation of a later step where Calendars depends on
// TaskLibrary to schedule templates -- same ordering discipline as Guardians before the features
// that need IGuardianLinkEventStore.
builder.Services.AddTaskLibraryFeature(builder.Configuration);
builder.Services.AddCalendarsFeature(builder.Configuration);
builder.Services.AddMedicinesFeature(builder.Configuration);
builder.Services.AddMealplansFeature(builder.Configuration);
// Before Pickups: AssignPickup and ListPickupSchedule read IBabysitterListEventStore.
builder.Services.AddBabysittersFeature(builder.Configuration);
builder.Services.AddPickupsFeature(builder.Configuration);
builder.Services.AddWorkLocationsFeature(builder.Configuration);
// After Guardians, Groups, Calendars and WorkLocations: its write-time reference checks read their stores.
builder.Services.AddPrintTemplatesFeature(builder.Configuration);
builder.Services.AddProgressFeature(builder.Configuration);
// After Users and Guardians: authorization reads IGuardianLinkEventStore, the shared view IUserEventStore.
builder.Services.AddSleepDiariesFeature(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Skipped in Development: the frontend calls the plain-http Kestrel endpoint, and redirecting
    // to https here would break CORS preflight (redirects aren't valid preflight responses).
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
// Before the idempotency middleware, so an unprovisioned caller never reserves a key.
app.UseProvisionedUsers();
// Outside UseIdempotencyKeys: a request that lost a concurrency race gets 409 and its
// Idempotency-Key released, so the client can retry it with the same key.
app.UseConcurrencyConflicts();
// Also outside UseIdempotencyKeys, for the same reason: a rejected body releases its key.
app.UseRequestBindingFailures();
app.UseIdempotencyKeys();

app.MapHealthChecks("/health");

app.MapUsersFeature();
app.MapGuardiansFeature();
app.MapGroupsFeature();
app.MapTaskLibraryFeature();
app.MapCalendarsFeature();
app.MapMedicinesFeature();
app.MapMealplansFeature();
app.MapBabysittersFeature();
app.MapPickupsFeature();
app.MapWorkLocationsFeature();
app.MapPrintTemplatesFeature();
app.MapProgressFeature();
app.MapSleepDiariesFeature();

await app.RunAsync();
