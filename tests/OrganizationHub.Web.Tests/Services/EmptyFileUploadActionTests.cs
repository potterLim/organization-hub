using System.Collections.Generic;
using System.Reflection;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class EmptyFileUploadActionTests
{
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(true, "", "/upload?root=team%20folder")]
    [InlineData(false, "", "")]
    [InlineData(true, "없는 파일 검색", "")]
    public void EmptyActionRequiresUploadPermissionAndAnUnfilteredFolder(bool canUpload, string keyword, string expectedHref)
    {
        DriveFileBrowser browser = new DriveFileBrowser();
        typeof(DriveFileBrowser).GetField("mCanUploadFiles", FLAGS)!.SetValue(browser, canUpload);
        typeof(DriveFileBrowser).GetField("mKeyword", FLAGS)!.SetValue(browser, keyword);
        typeof(DriveFileBrowser).GetField("mSelectedRootFolderId", FLAGS)!.SetValue(browser, "team folder");
        Assert.Equal(expectedHref, typeof(DriveFileBrowser).GetMethod("getEmptyUploadHref", FLAGS)!.Invoke(browser, null));
    }

    [Theory]
    [InlineData("second", "second")]
    [InlineData("unreadable-folder", "first")]
    public void UploadEntrySelectsOnlyAnAuthorizedRoot(string requestedRoot, string expectedRoot)
    {
        Upload page = new Upload();
        page.RootFolderIdOrNull = requestedRoot;
        List<DriveFolderReference> folders = new List<DriveFolderReference>();
        folders.Add(createFolder("first"));
        folders.Add(createFolder("second"));
        typeof(Upload).GetField("mUploadRootFolders", FLAGS)!.SetValue(page, folders);
        DriveFolderReference selected = (DriveFolderReference)typeof(Upload).GetMethod("getDefaultUploadRootFolder", FLAGS)!.Invoke(page, null)!;
        Assert.Equal(expectedRoot, selected.FolderId.GetRequiredValue());
    }

    private static DriveFolderReference createFolder(string id)
    {
        DriveFolderReferenceCreationRequest request = new DriveFolderReferenceCreationRequest(DriveFolderId.CreateOptional(id), id, EDriveFolderKind.TeamMaterials, EDriveFolderUploadAccess.UploadAllowed);
        return new DriveFolderReference(request);
    }
}
