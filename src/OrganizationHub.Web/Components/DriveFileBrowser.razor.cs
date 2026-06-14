using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components;

public partial class DriveFileBrowser
{
    [Parameter]
    public EDriveFolderKind FolderKind
    {
        get; set;
    }

    [Parameter]
    public bool ShowTeam { get; set; } = true;

    [Parameter]
    public bool ShowPreview
    {
        get; set;
    }

    [Parameter]
    public bool EnableSearch
    {
        get; set;
    }

    [Parameter]
    public bool ManagementTeamOnly
    {
        get; set;
    }

    [Parameter]
    public bool ExcludeManagementTeam
    {
        get; set;
    }

    [Parameter]
    public string EmptyMessage { get; set; } = "표시할 파일이 없습니다.";

    [Parameter]
    public string UploadedByLabel { get; set; } = "올린 사람";

    [Parameter]
    public string SearchPlaceholder { get; set; } = "파일명 검색";

    [Parameter]
    public string RootFolderLabel { get; set; } = "폴더 선택";

    [Parameter]
    public string ErrorMessagePrefix { get; set; } = "자료를 불러오지 못했습니다.";

    private List<DriveFolderReference> mRootFolders = new List<DriveFolderReference>();
    private List<DriveFolderPathItem> mFolderPath = new List<DriveFolderPathItem>();
    private List<DriveFileItem> mFiles = new List<DriveFileItem>();
    private string mSelectedRootFolderId = string.Empty;
    private string mKeyword = string.Empty;
    private string mRootErrorMessage = string.Empty;
    private string mFileErrorMessage = string.Empty;
    private DriveFileItem? mDeleteCandidateOrNull;
    private ElementReference mRefreshScopeElement;
    private ChangeVersionSnapshot mLoadedChangeSnapshot = new ChangeVersionSnapshot();
    private IJSObjectReference? mRefreshModuleOrNull;
    private DotNetObjectReference<DriveFileBrowser>? mRefreshDotNetReferenceOrNull;
    private bool mIsLoadingRoots;
    private bool mIsLoadingFiles;
    private bool mCanDeleteFiles;
    private bool mIsDeleting;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession)
        {
            await loadRootFoldersAsync();
        }
    }
}
