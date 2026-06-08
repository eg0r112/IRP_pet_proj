using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace irp_pet.Infrastructure;

/// <summary>
/// OpenApi 2.x: без document в reference Authorize в Swagger UI не отправляет заголовки.
/// </summary>
public sealed class SwaggerSecurityDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var (path, pathItem) in swaggerDoc.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations)
            {
                if (IsAnonymousAuthPath(path))
                {
                    operation.Security = null;
                    continue;
                }

                if (path.Contains("/alerts", StringComparison.OrdinalIgnoreCase))
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("ApiKey", swaggerDoc)] = []
                        }
                    ];
                    continue;
                }

                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", swaggerDoc)] = []
                    }
                ];
            }
        }
    }

    private static bool IsAnonymousAuthPath(string path) =>
        path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/auth/logout", StringComparison.OrdinalIgnoreCase);
}
