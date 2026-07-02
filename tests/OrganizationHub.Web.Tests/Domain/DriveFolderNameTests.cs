using System;
using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Domain;

public sealed class DriveFolderNameTests
{
    [Fact]
    public void CreateOptionalTrimsFolderName()
    {
        DriveFolderName folderName = DriveFolderName.CreateOptional("  월간 운영 자료  ");

        Assert.True(folderName.HasValue);
        Assert.Equal("월간 운영 자료", folderName.GetRequiredValue());
    }

    [Fact]
    public void CreateOptionalRejectsTooLongFolderName()
    {
        string folderNameValue = new string('가', DriveFolderName.MAXIMUM_LENGTH + 1);

        DriveFolderName folderName = DriveFolderName.CreateOptional(folderNameValue);

        Assert.False(folderName.HasValue);
    }

    [Theory]
    [InlineData("상반기/운영")]
    [InlineData("상반기\\운영")]
    public void CreateOptionalRejectsPathSeparators(string folderNameValue)
    {
        DriveFolderName folderName = DriveFolderName.CreateOptional(folderNameValue);

        Assert.False(folderName.HasValue);
    }

    [Fact]
    public void ToDriveItemNameCreatesRequiredDriveItemName()
    {
        DriveFolderName folderName = DriveFolderName.CreateRequired("월간 운영 자료");

        DriveItemName driveItemName = folderName.ToDriveItemName();

        Assert.Equal("월간 운영 자료", driveItemName.GetRequiredValue());
    }

    [Fact]
    public void CreateRequiredThrowsWhenFolderNameIsInvalid()
    {
        string folderNameValue = new string('가', DriveFolderName.MAXIMUM_LENGTH + 1);

        Assert.Throws<ArgumentException>(() => DriveFolderName.CreateRequired(folderNameValue));
    }
}
