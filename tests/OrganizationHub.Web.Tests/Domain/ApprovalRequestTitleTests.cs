using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Domain;

public sealed class ApprovalRequestTitleTests
{
    [Fact]
    public void GetValueOrFallbackUsesTrimmedCustomTitle()
    {
        ApprovalRequestTitle title = ApprovalRequestTitle.CreateOptional("  검토 문서  ");
        DriveItemName fallbackFileName = DriveItemName.CreateRequired("기본 파일.pdf");

        string resolvedTitle = title.GetValueOrFallback(fallbackFileName);

        Assert.Equal("검토 문서", resolvedTitle);
    }

    [Fact]
    public void GetValueOrFallbackUsesFileNameWhenCustomTitleIsEmpty()
    {
        ApprovalRequestTitle title = ApprovalRequestTitle.Empty;
        DriveItemName fallbackFileName = DriveItemName.CreateRequired("기본 파일.pdf");

        string resolvedTitle = title.GetValueOrFallback(fallbackFileName);

        Assert.Equal("기본 파일.pdf", resolvedTitle);
    }

    [Fact]
    public void GetValueOrFallbackLimitsStoredTitleLength()
    {
        string longTitle = new string('가', ApprovalRequestTitle.MAXIMUM_LENGTH + 10);
        ApprovalRequestTitle title = ApprovalRequestTitle.CreateOptional(longTitle);
        DriveItemName fallbackFileName = DriveItemName.CreateRequired("기본 파일.pdf");

        string resolvedTitle = title.GetValueOrFallback(fallbackFileName);

        Assert.Equal(ApprovalRequestTitle.MAXIMUM_LENGTH, resolvedTitle.Length);
    }
}
