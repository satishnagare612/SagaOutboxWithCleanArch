using SagaOutBoxCleanArch.Application;
using SagaOutBoxCleanArch.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddInventoryApplication();
builder.Services.AddInventoryInfrastructure(builder.Configuration);

await builder.Build().RunAsync();
