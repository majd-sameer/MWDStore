using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Store.Application.DevAssistant;

namespace Store.Api.Infrastructure;

/// <summary>
/// One-time reflection scan of the API assembly for the Developer Assistant (spec §2.3 Source B):
/// harvests every controller action's route, verb, policy and audit participation. Lives in the API
/// layer because only it can see its own controller types; the Application layer consumes the
/// <see cref="IApiSurfaceSource"/> abstraction.
/// </summary>
public sealed class ReflectionApiSurfaceSource : IApiSurfaceSource
{
    private readonly Assembly _assembly;

    public ReflectionApiSurfaceSource(Assembly assembly)
    {
        _assembly = assembly;
    }

    public string AssemblyVersion =>
        _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? _assembly.GetName().Version?.ToString()
        ?? "unknown";

    public IReadOnlyList<ApiEndpointDescriptor> Scan(ICollection<string> notices)
    {
        var endpoints = new List<ApiEndpointDescriptor>();

        foreach (var type in _assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ControllerBase).IsAssignableFrom(type))
                continue;

            try
            {
                ScanController(type, endpoints);
            }
            catch (Exception ex)
            {
                // SEC-15: one unreadable controller degrades the route catalog, never startup.
                notices.Add($"Controller '{type.Name}' could not be reflected: {ex.Message}");
            }
        }

        return endpoints
            .OrderBy(e => e.Route, StringComparer.Ordinal)
            .ThenBy(e => e.Verb, StringComparer.Ordinal)
            .ThenBy(e => e.Action, StringComparer.Ordinal)
            .ToList();
    }

    private static void ScanController(Type type, List<ApiEndpointDescriptor> endpoints)
    {
        var controllerName = type.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? type.Name[..^"Controller".Length]
            : type.Name;
        var controllerRoute = type.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
        var controllerPolicy = type.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
        var controllerAnonymous = type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
        var controllerSkipAudit = type.GetCustomAttribute<SkipAuditAttribute>() is not null;

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var httpAttribute in method.GetCustomAttributes<HttpMethodAttribute>())
            {
                var route = Combine(controllerRoute, httpAttribute.Template)
                    .Replace("[controller]", controllerName.ToLowerInvariant(), StringComparison.Ordinal);
                var methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>();

                endpoints.Add(new ApiEndpointDescriptor(
                    controllerName,
                    method.Name,
                    httpAttribute.HttpMethods.FirstOrDefault() ?? "GET",
                    route,
                    methodAuthorize?.Policy ?? controllerPolicy,
                    controllerAnonymous || method.GetCustomAttribute<AllowAnonymousAttribute>() is not null,
                    controllerSkipAudit || method.GetCustomAttribute<SkipAuditAttribute>() is not null,
                    FirstComplexParameter(method),
                    UnwrapResponseType(method.ReturnType)));
            }
        }
    }

    private static string Combine(string controllerTemplate, string? actionTemplate)
    {
        var head = controllerTemplate.Trim('/');
        var tail = actionTemplate?.Trim('/') ?? string.Empty;
        if (tail.Length == 0)
            return head;
        if (tail.StartsWith('/') || head.Length == 0)
            return tail;
        return head + "/" + tail;
    }

    private static string? FirstComplexParameter(MethodInfo method) =>
        method.GetParameters()
            .Select(p => p.ParameterType)
            .FirstOrDefault(t => t.IsClass && t != typeof(string) && !t.IsArray
                                 && t.Namespace is not null
                                 && !t.Namespace.StartsWith("Microsoft", StringComparison.Ordinal)
                                 && !t.Namespace.StartsWith("System", StringComparison.Ordinal))
            ?.Name;

    private static string? UnwrapResponseType(Type returnType)
    {
        var type = returnType;
        while (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>)
                || definition.Name == "ActionResult`1")
            {
                type = type.GetGenericArguments()[0];
                continue;
            }
            break;
        }
        return type == typeof(void) || typeof(IActionResult).IsAssignableFrom(type) || type == typeof(Task)
            ? null
            : PrettyName(type);
    }

    private static string PrettyName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;
        var arguments = string.Join(", ", type.GetGenericArguments().Select(PrettyName));
        return $"{type.Name[..type.Name.IndexOf('`')]}<{arguments}>";
    }
}
