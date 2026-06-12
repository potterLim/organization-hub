using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IFilePolicyService
{
    FileUploadPolicy GetUploadPolicy(EDriveFolderKind folderKind);
    FileUploadPolicy GetUploadPolicy(EDriveFolderKind folderKind, EFileUploadPolicyContext uploadPolicyContext);
    bool CanPreviewInSite(string mimeType, string fileName);
    string GetContentType(string fileName);
}
