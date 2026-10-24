using SagaOutBoxCleanArch.Application;
using SagaOutBoxCleanArch.Infrastructure;
using SagaOutBoxCleanArch.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddOrderApplication();
builder.Services.AddOrderInfrastructure(builder.Configuration);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
if (!ServiceBusRegistration.IsConfigured(builder.Configuration))
{
    app.Logger.LogWarning("Azure Service Bus is not configured; order commands and saga-start messages remain in the database outbox.");
}
await app.RunAsync();
