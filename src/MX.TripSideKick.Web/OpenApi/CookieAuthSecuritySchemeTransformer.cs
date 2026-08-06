using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MX.TripSideKick.Web.OpenApi;

/// <summary>
/// Documents the app surface's authentication model in the generated OpenAPI document: a
/// same-origin session cookie established via the OpenID Connect sign-in flow. There is no
/// bearer token accepted by the API - this scheme exists purely for documentation/tooling
/// completeness (e.g. static-analysis checks that expect every OpenAPI document to declare a
/// security scheme and a global security requirement).
/// </summary>
public sealed class CookieAuthSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "SessionCookie";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = ".AspNetCore.Cookies",
                Description = "Same-origin session cookie established by the OpenID Connect sign-in flow. " +
                    "Mutating requests additionally require the X-CSRF-TOKEN header (see /v1/auth/antiforgery).",
            },
        };

        // Apply the requirement document-wide (global) and to every operation, so both the
        // top-level "security" field and each operation's security are populated.
        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        };

        document.Security = [requirement];

        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations!.Values))
        {
            operation.Security ??= [];
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    }
}
