using OrganizationHub.Web.Components;
using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ComponentRouteMatcherTests
{
    [Theory]
    [InlineData("/dashboard", true)]
    [InlineData("/admin/users", true)]
    [InlineData("/not-found", true)]
    [InlineData("/does-not-exist", false)]
    [InlineData("/admin/nonexistent", false)]
    public void MissingRoutesRemainDistinctFromForbiddenPages(string path, bool isKnown)
    {
        Assert.Equal(isKnown, ComponentRouteMatcher.IsKnownPath(NavigationPath.CreateOptional(path)));
    }
}
