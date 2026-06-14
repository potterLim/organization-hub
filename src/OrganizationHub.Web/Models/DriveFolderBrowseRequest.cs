using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class DriveFolderBrowseRequest
{
    public UserSession UserSession
    {
        get; private init;
    }
    public EDriveFolderKind FolderKind
    {
        get; private init;
    }
    public DriveFolderId RootFolderId
    {
        get; private init;
    }
    public DriveFolderId FolderId
    {
        get; private init;
    }
    public SearchKeyword Keyword { get; set; } = SearchKeyword.Empty;

    public DriveFolderBrowseRequest(UserSession userSession, EDriveFolderKind folderKind, DriveFolderId rootFolderId, DriveFolderId folderId)
    {
        UserSession = userSession;
        FolderKind = folderKind;
        RootFolderId = rootFolderId;
        FolderId = folderId;
    }
}
