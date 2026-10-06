using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eventely.Api.Common;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionAttributes = context.MethodInfo.GetCustomAttributes(true);
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? [];
        if (actionAttributes.OfType<AllowAnonymousAttribute>().Any() ||
            controllerAttributes.OfType<AllowAnonymousAttribute>().Any())
        {
            return;
        }

        var requiresAuthorization = actionAttributes.OfType<AuthorizeAttribute>().Any() ||
                                    controllerAttributes.OfType<AuthorizeAttribute>().Any();
        if (!requiresAuthorization)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });
    }
}
