using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Domain;

public sealed class OrganizationHubProductTests
{
    [Theory]
    [InlineData("홈", "홈 · Organization Hub")]
    [InlineData("일정", "일정 · Organization Hub")]
    [InlineData(" ", "Organization Hub")]
    public void PageTitleIdentifiesTheCurrentScreen(string pageName, string expectedTitle)
    {
        Assert.Equal(expectedTitle, OrganizationHubProduct.FormatPageTitle(pageName));
    }
}
