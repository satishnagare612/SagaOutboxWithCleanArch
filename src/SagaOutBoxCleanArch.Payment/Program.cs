using SagaOutBoxCleanArch.Application;
using SagaOutBoxCleanArch.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddPaymentApplication();
builder.Services.AddPaymentInfrastructure(builder.Configuration);

await builder.Build().RunAsync();
