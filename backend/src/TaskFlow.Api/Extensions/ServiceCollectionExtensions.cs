using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Services;
using TaskFlow.Api.Swagger;
using TaskFlow.Application.Common;
using TaskFlow.Infrastructure.Identity;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{

  public static IServiceCollection AddApiServices(this IServiceCollection services)
  {
    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();
    services.AddSingleton<IBoardNotifier, SignalRBoardNotifier>();

    services
      .AddControllers()
      .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    services
      .AddSignalR()
      .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");
    return services;
  }

  public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
  {
    services
      .AddOptions<JwtOptions>()
      .Validate(a => a.Key is { Length: >= 32 }, "Jwt:Key must be set (at least 32 characters).")
      .ValidateOnStart();

    services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer();

    // Configured lazily so the key is read from the final configuration (and tests can override it).
    services
      .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
      .Configure<IOptions<JwtOptions>>((options, jwt) =>
      {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = jwt.Value.Issuer,
          ValidateAudience = true,
          ValidAudience = jwt.Value.Audience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key)),
          ValidateLifetime = true,
          ClockSkew = TimeSpan.FromSeconds(30),
          NameClaimType = "name"
        };

        // Browsers cannot set headers on WebSocket / SSE requests, so SignalR sends the token in the query string.
        options.Events = new JwtBearerEvents
        {
          OnMessageReceived = context =>
          {
            var accessToken = context.Request.Query["access_token"];
            if (string.IsNullOrEmpty(accessToken) == false && context.HttpContext.Request.Path.StartsWithSegments(BoardHub.Path))
            {
              context.Token = accessToken;
            }

            return Task.CompletedTask;
          }
        };
      });

    services.AddAuthorization();
    return services;
  }

  public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
  {
    var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
    return services.AddCors(options => options.AddPolicy(ApiPolicies.Cors, policy => policy
      .WithOrigins(origins)
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials()));
  }

  public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
  {
    return services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      // The limit is read per request so it can be changed through configuration (and overridden in tests).
      options.AddPolicy(ApiPolicies.AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimiting:AuthPermitLimit", 10),
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0
        }));

      options.OnRejected = async (context, cancellationToken) =>
      {
        var problem = new ProblemDetails
        {
          Type = "https://httpstatuses.com/429",
          Title = "Too Many Requests",
          Status = StatusCodes.Status429TooManyRequests,
          Detail = "Too many attempts. Please wait a moment and try again."
        };
        problem.Extensions["code"] = "rate_limit.exceeded";
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions)null, "application/problem+json", cancellationToken);
      };
    });
  }

  public static IServiceCollection AddApiVersioningWithExplorer(this IServiceCollection services)
  {
    services
      .AddApiVersioning(options =>
      {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
      })
      .AddMvc()
      .AddApiExplorer(options =>
      {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
      });

    return services;
  }

  public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
  {
    services.AddEndpointsApiExplorer();
    return services.AddSwaggerGen(options =>
    {
      options.SwaggerDoc("v1", new OpenApiInfo { Title = "TaskFlow API", Version = "v1" });
      options.SupportNonNullableReferenceTypes();
      options.SchemaFilter<RequireNonNullablePropertiesSchemaFilter>();
      options.CustomOperationIds(api =>
        $"{api.ActionDescriptor.RouteValues["controller"]}_{api.ActionDescriptor.RouteValues["action"]}");

      options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
      {
        Description = "JWT access token. Paste the token only; the Bearer prefix is added automatically.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
      });
      options.AddSecurityRequirement(new OpenApiSecurityRequirement
      {
        {
          new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
          Array.Empty<string>()
        }
      });
    });
  }
}
