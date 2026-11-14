using Serilog;
using TaskFlow.Api.Extensions;
using TaskFlow.Application;
using TaskFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
  .ReadFrom.Configuration(context.Configuration)
  .Enrich.FromLogContext()
  .WriteTo.Console());

builder.Services
  .AddApplication()
  .AddInfrastructure(builder.Configuration)
  .AddApiServices()
  .AddJwtAuthentication()
  .AddCorsPolicy(builder.Configuration)
  .AddApiRateLimiting()
  .AddApiVersioningWithExplorer()
  .AddSwaggerWithJwt();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app
  .UseApiPipeline()
  .MapApiEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
