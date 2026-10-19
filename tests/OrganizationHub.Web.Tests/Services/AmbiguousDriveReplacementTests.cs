using System;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class AmbiguousDriveReplacementTests
{
    [Fact]
    public void MultipleSameNameFilesCannotSilentlySelectFirstReplacement()
    {
        DriveFileItem first = new DriveFileItem();
        first.Id = "first";
        DriveFileItem second = new DriveFileItem();
        second.Id = "second";
        DriveFileNameConflictCheckResult conflict = new DriveFileNameConflictCheckResult(DriveItemName.CreateRequired("document.pdf"), DriveItemName.CreateRequired("document (2).pdf"), new DriveFileItem[] { first, second });
        Assert.Throws<InvalidOperationException>(() => conflict.GetFirstExistingFileId());
    }
}
