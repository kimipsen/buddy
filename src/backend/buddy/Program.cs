using buddy.Email;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Mealplans;
using buddy.Features.Medicines;
using buddy.Features.Pickups;
using buddy.Features.Users;
using buddy.Serialization;

using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(opts =>
{
    // IKeycloakAdminClient is registered via AddHttpClient<TClient, TImpl>(), which wires it up
    // through HttpClientFactory's internal "opaque" lambda factory -- Wolverine can't inline that
    // into generated constructor code, so it needs the explicit service-location opt-in below
    // (the rest of each handler's dependencies still get the faster constructor-inlined codegen).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<IKeycloakAdminClient>();
});

// Add services to the container.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new StronglyTypedIdJsonConverterFactory());
});

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
builder.Services.AddEmail(builder.Configuration);
builder.Services.AddUsersFeature(builder.Configuration);
builder.Services.AddGuardiansFeature(builder.Configuration);
builder.Services.AddGroupsFeature(builder.Configuration);
builder.Services.AddCalendarsFeature(builder.Configuration);
builder.Services.AddMedicinesFeature(builder.Configuration);
builder.Services.AddMealplansFeature(builder.Configuration);
builder.Services.AddPickupsFeature(builder.Configuration);

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

app.MapUsersFeature();
app.MapGuardiansFeature();
app.MapGroupsFeature();
app.MapCalendarsFeature();
app.MapMedicinesFeature();
app.MapMealplansFeature();
app.MapPickupsFeature();

app.Run();
