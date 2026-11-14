using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Identity;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.Section));
    services.TryAddSingleton(TimeProvider.System);

    services.AddDbContext<ApplicationDbContext>(options => options.UseConfiguredProvider(configuration));
    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

    services
      .AddIdentityCore<User>(options =>
      {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
      })
      .AddEntityFrameworkStores<ApplicationDbContext>();

    services.AddScoped<IIdentityService, IdentityService>();
    services.AddSingleton<IAuthTokenService, AuthTokenService>();
    services.AddScoped<DemoDataSeeder>();
    return services;
  }

  /// <summary>
  /// Selects the EF provider from <c>Database:Provider</c> (Sqlite | SqlServer | PostgreSql).
  /// The migrations shipped in this repository target SQLite; another provider needs its own migration set.
  /// </summary>
  public static DbContextOptionsBuilder UseConfiguredProvider(this DbContextOptionsBuilder options, IConfiguration configuration)
  {
    var provider = configuration["Database:Provider"] ?? "Sqlite";
    var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=taskflow.db";

    switch (provider.ToLowerInvariant())
    {
      case "sqlite":
        options.UseSqlite(connectionString);
        break;
      case "sqlserver":
        options.UseSqlServer(connectionString);
        break;
      case "postgresql":
      case "postgres":
        options.UseNpgsql(connectionString);
        break;
      default:
        throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
    }

    // Query filters on required navigations are intentional (soft delete); dependents are always read through their parents.
    return options.ConfigureWarnings(a => a.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
  }
}
