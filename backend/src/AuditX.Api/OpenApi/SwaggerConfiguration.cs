using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuditX.Api.OpenApi;

/// <summary>
/// Configures a complete, accurate OpenAPI/Swagger document for AuditX: API metadata, the bearer/session-cookie
/// security scheme, controller + DTO XML summaries, snake_case enums rendered as strings, and the standard
/// RFC 7807 error responses every endpoint can return. Registered from <c>Program.cs</c>.
/// </summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddAuditXOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AuditX Enterprise API",
                Version = "v1",
                Description =
                    "On-premises internal-audit platform for banks (M1-M15). All successful responses use the "
                    + "`{ data, metadata }` envelope; errors use RFC 7807 `application/problem+json` "
                    + "(422 validation with `field_errors`, 409 conflict, 403 forbidden, 404 not found, 401 "
                    + "unauthenticated). Collection endpoints use opaque-cursor pagination "
                    + "(`?cursor=&limit=`, default 20 / max 100). Enum values are snake_case. "
                    + "Authentication is an HttpOnly session cookie (`auditx.session`) issued after Active Directory "
                    + "sign-in; the same JWT is also accepted as a `Bearer` token for API clients.",
                Contact = new OpenApiContact { Name = "AuditX Platform Team" },
            });

            // Bearer (the session JWT is also accepted in the Authorization header — convenient for Swagger UI / clients).
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the session JWT (issued at /api/v1/auth/login). The browser SPA uses the "
                    + "HttpOnly `auditx.session` cookie instead and never sees the token.",
            });
            options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>(),
            });

            // Include XML doc comments from the API (controllers + request contracts) and Application (DTOs).
            foreach (var assembly in new[] { typeof(Program).Assembly, typeof(AuditX.Application.DependencyInjection).Assembly })
            {
                var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xml))
                {
                    options.IncludeXmlComments(xml, includeControllerXmlComments: true);
                }
            }

            // Fully-qualified schema ids so same-named DTOs across modules (e.g. multiple *Dto) never collide.
            options.CustomSchemaIds(t => t.FullName!.Replace("+", ".", StringComparison.Ordinal));

            options.SupportNonNullableReferenceTypes();
            options.SchemaFilter<SnakeCaseEnumSchemaFilter>();
            options.OperationFilter<StandardErrorResponsesOperationFilter>();
        });

        return services;
    }
}

/// <summary>Render enums as their snake_case string values (the platform serializes enums as snake_case, not ints).</summary>
public sealed class SnakeCaseEnumSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum || schema is not OpenApiSchema concrete)
        {
            return;
        }

        concrete.Type = JsonSchemaType.String;
        concrete.Format = null;
        concrete.Enum = Enum.GetNames(context.Type)
            .Select(name => (JsonNode)JsonValue.Create(
                AuditX.Application.Common.Enums.EnumExtensions.ToSnake((Enum)Enum.Parse(context.Type, name)))!)
            .ToList();
    }
}

/// <summary>Document the RFC 7807 error responses every secured endpoint can return, so the spec is complete.</summary>
public sealed class StandardErrorResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Generate (and register) the ProblemDetails schema so the $ref resolves instead of dangling.
        var problemSchema = context.SchemaGenerator.GenerateSchema(
            typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), context.SchemaRepository);
        operation.Responses ??= new OpenApiResponses();

        void Add(string code, string description)
            => operation.Responses.TryAdd(code, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = problemSchema },
                },
            });

        Add("401", "Unauthenticated - no valid session.");
        Add("403", "Forbidden - the caller lacks the required permission or resource scope.");
        Add("422", "Validation failed - see `field_errors`.");

        // Endpoints that take a route/path id or a body can also yield 404/409.
        var hasIdOrBody = context.ApiDescription.ParameterDescriptions.Any(p => p.Source.Id is "Path" or "Body");
        if (hasIdOrBody)
        {
            Add("404", "The referenced resource was not found.");
            Add("409", "Conflict - concurrency or a state/uniqueness invariant was violated.");
        }
    }
}
