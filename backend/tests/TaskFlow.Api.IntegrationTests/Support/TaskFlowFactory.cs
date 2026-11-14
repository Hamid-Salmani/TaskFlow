using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.IntegrationTests.Support;

/// <summary>
/// Boots the real application against SQLite in memory. The connection is opened once and shared, otherwise every
/// scope would get its own empty database. The real migrations are applied on startup, so they are tested too.
/// A single shared connection cannot run two transactions at once; tests that need real concurrent requests use
/// <see cref="FileDatabaseFactory"/> instead.
/// </summary>
public class TaskFlowFactory : WebApplicationFactory<Program>
{
  private readonly object _gate = new();
  private SqliteConnection? _connection;

  /// <summary>Called for every DbContext that is created, so anything shared (the connection) must be created once.</summary>
  protected virtual void ConfigureDatabase(DbContextOptionsBuilder options)
  {
    lock (_gate)
    {
      if (_connection is null)
      {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
      }
    }

    options.UseSqlite(_connection);
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Testing");
    builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789abcdef");
    builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
      services.AddDbContext<ApplicationDbContext>(options =>
      {
        ConfigureDatabase(options);
        options.ConfigureWarnings(a => a.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
      });
    });
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing)
    {
      _connection?.Dispose();
    }
  }
}

/// <summary>
/// Same application on a temporary SQLite file: every request gets its own connection, so simultaneous requests really
/// race each other (the concurrency token, not a shared connection, decides who wins).
/// </summary>
public sealed class FileDatabaseFactory : TaskFlowFactory
{
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-tests-{Guid.NewGuid():N}.db");

  protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
    options.UseSqlite($"Data Source={_path};Default Timeout=30");

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing)
    {
      SqliteConnection.ClearAllPools();
      foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_path) + "*"))
      {
        File.Delete(file);
      }
    }
  }
}
