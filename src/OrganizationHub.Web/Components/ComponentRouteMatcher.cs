using System;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components;

internal static class ComponentRouteMatcher
{
    private static readonly TemplateMatcher[] ROUTE_MATCHERS = typeof(App).Assembly.GetTypes()
        .SelectMany(componentType => componentType.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
        .Select(route => new TemplateMatcher(TemplateParser.Parse(route.Template), new RouteValueDictionary()))
        .ToArray();

    public static bool IsKnownPath(NavigationPath path)
    {
        PathString requestPath = new PathString(path.GetRequiredValue());
        return ROUTE_MATCHERS.Any(matcher => matcher.TryMatch(requestPath, new RouteValueDictionary()));
    }
}
