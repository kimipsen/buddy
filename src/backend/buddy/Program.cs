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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.ShouldInclude = api => api.GroupName is null;
});
builder.Services.AddUsersFeature(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapUsersFeature();

app.Run();
