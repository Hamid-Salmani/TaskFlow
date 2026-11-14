using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Auth;
using TaskFlow.Application.Ordering;

namespace TaskFlow.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    var assembly = typeof(DependencyInjection).Assembly;

    services.AddMediatR(config =>
    {
      config.RegisterServicesFromAssembly(assembly);
      // Registration order = execution order: authorize first, then validate.
      config.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
      config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });

    services.AddValidatorsFromAssembly(assembly);
    services.AddSingleton<IPositionService, PositionService>();
    services.AddScoped<IAccessChecker, AccessChecker>();
    services.AddScoped<AuthorizationContext>();
    services.AddScoped<IAuthorizationContext>(sp => sp.GetRequiredService<AuthorizationContext>());
    services.AddScoped<ActivityRecorder>();
    services.AddScoped<SessionIssuer>();
    return services;
  }
}
