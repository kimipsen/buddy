using buddy.Email;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;
using buddy.Serialization;

using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine();

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
builder.Services.AddGroupsFeature(builder.Configuration);
builder.Services.AddCalendarsFeature(builder.Configuration);

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
app.MapGroupsFeature();
app.MapCalendarsFeature();

app.Run();
