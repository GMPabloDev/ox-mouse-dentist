using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace MouseDent.Api.Common;

public static class ApiDocumentation
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "MouseDent API",
                    Version = "v1",
                    Description = "Panel interno y reserva pública de MouseDent.",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token emitido por POST /api/auth/login.",
                };

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAuthorizeData>()
                    .Any();

                if (requiresAuthorization)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Expone el documento OpenAPI y la interfaz Scalar. En Development siempre;
    /// fuera de Development requiere <c>Api:EnableDocumentation=true</c>.
    /// </summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        var enabled = app.Environment.IsDevelopment()
            || app.Configuration.GetValue("Api:EnableDocumentation", false);

        if (!enabled)
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle("MouseDent API")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

        return app;
    }
}
