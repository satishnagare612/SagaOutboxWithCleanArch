using SagaOutBoxCleanArch.Application;
using SagaOutBoxCleanArch.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddShippingApplication();
builder.Services.AddShippingInfrastructure(builder.Configuration);

await builder.Build().RunAsync();
