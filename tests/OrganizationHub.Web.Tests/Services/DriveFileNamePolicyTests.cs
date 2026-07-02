using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DriveFileNamePolicyTests
{
    [Fact]
    public void CreateAvailableFileNameAddsNumberedSuffixWhenSameFileNameExists()
    {
        IReadOnlyList<DriveFileItem> existingItems = new List<DriveFileItem>
        {
            createFile("고객지원 이슈 대응 계획.txt"),
            createFile("고객지원 이슈 대응 계획 (2).txt")
        };

        DriveItemName availableFileName = DriveFileNamePolicy.CreateAvailableFileName(
            DriveItemName.CreateRequired("고객지원 이슈 대응 계획.txt"),
            existingItems);

        Assert.Equal("고객지원 이슈 대응 계획 (3).txt", availableFileName.GetRequiredValue());
    }

    [Fact]
    public void CreateAvailableFileNameKeepsRequestedNameWhenNoConflictExists()
    {
        IReadOnlyList<DriveFileItem> existingItems = new List<DriveFileItem>
        {
            createFile("월간 운영 회의록.txt")
        };

        DriveItemName availableFileName = DriveFileNamePolicy.CreateAvailableFileName(
            DriveItemName.CreateRequired("고객지원 이슈 대응 계획.txt"),
            existingItems);

        Assert.Equal("고객지원 이슈 대응 계획.txt", availableFileName.GetRequiredValue());
    }

    private static DriveFileItem createFile(string fileName)
    {
        DriveFileItem fileItem = new DriveFileItem();
        fileItem.Id = fileName;
        fileItem.Name = fileName;
        fileItem.MimeType = "text/plain";
        fileItem.IsFolder = false;
        return fileItem;
    }
}
