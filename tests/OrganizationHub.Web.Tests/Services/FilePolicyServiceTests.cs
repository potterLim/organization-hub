using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class FilePolicyServiceTests
{
    [Theory]
    [InlineData("회의록.pdf", "application/pdf")]
    [InlineData("계획.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("정산.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("발표.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("지도.png", "image/png")]
    [InlineData("사진.jpg", "image/jpeg")]
    [InlineData("사진.jpeg", "image/jpeg")]
    [InlineData("자료.zip", "application/zip")]
    [InlineData("메모.txt", "text/plain")]
    [InlineData("규칙.md", "text/markdown")]
    public void GetContentTypeReturnsServerDefinedType(string fileName, string expectedContentType)
    {
        OrganizationHubOptions options = new OrganizationHubOptions();
        FilePolicyService filePolicyService = new FilePolicyService(Options.Create(options));

        FileContentType contentType = filePolicyService.GetContentType(DriveItemName.CreateRequired(fileName));

        Assert.Equal(expectedContentType, contentType.GetRequiredValue());
    }
}
