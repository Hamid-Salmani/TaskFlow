using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TaskFlow.Api.Swagger;

/// <summary>
/// Marks every non-nullable property as required. Without it the generated TypeScript types would make all
/// fields optional (<c>name?: string</c>) because Swashbuckle only emits "required" for [Required] properties.
/// </summary>
public sealed class RequireNonNullablePropertiesSchemaFilter : ISchemaFilter
{
  public void Apply(OpenApiSchema schema, SchemaFilterContext context)
  {
    if (schema.Properties is null || schema.Properties.Count == 0)
    {
      return;
    }

    foreach (var (name, property) in schema.Properties)
    {
      if (property.Nullable == false)
      {
        schema.Required.Add(name);
      }
    }
  }
}
