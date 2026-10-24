using SagaOutBoxCleanArch.Application;
using SagaOutBoxCleanArch.Application.Sagas;
using SagaOutBoxCleanArch.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddSagaApplication();
builder.Services.AddSagaInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/sagas/{id:guid}", async (Guid id, ISagaQueries queries, CancellationToken cancellationToken) =>
{
    var status = await queries.GetStatusAsync(id, cancellationToken);
    return status is null ? Results.NotFound() : Results.Ok(status);
});

await app.RunAsync();
