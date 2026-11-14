using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Middleware;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.Extensions;

public static class WebApplicationExtensions
{
  public static WebApplication UseApiPipeline(this WebApplication app)
  {
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskFlow API v1"));
    }

    app.UseCors(ApiPolicies.Cors);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    return app;
  }

  public static WebApplication MapApiEndpoints(this WebApplication app)
  {
    app.MapControllers();
    app.MapHub<BoardHub>(BoardHub.Path);
    app.MapHealthChecks("/health");
    return app;
  }

  /// <summary>Applies pending migrations and seeds demo data when <c>Seed:Enabled</c> is true (Development only).</summary>
  public static async Task InitializeDatabaseAsync(this WebApplication app)
  {
    // Tooling that only needs the built host (OpenAPI export) opts out of touching the database.
    if (app.Configuration.GetValue("Database:SkipInitialization", false))
    {
      return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var seedEnabled = app.Environment.IsDevelopment() && app.Configuration.GetValue("Seed:Enabled", false);
    if (seedEnabled)
    {
      await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
  }
}
