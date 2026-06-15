using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class FilePolicyService : IFilePolicyService
{
    private static readonly IReadOnlySet<string> MINUTES_EXTENSIONS = new HashSet<string> { ".pdf" };
    private static readonly IReadOnlySet<string> MATERIAL_EXTENSIONS = new HashSet<string>
    {
        ".pdf",
        ".docx",
        ".xlsx",
        ".pptx",
        ".png",
        ".jpg",
        ".jpeg",
        ".zip",
        ".txt",
        ".md"
    };

    private readonly OrganizationHubOptions mOptions;

    public FilePolicyService(IOptions<OrganizationHubOptions> options)
    {
        mOptions = options.Value;
    }

    public FileUploadPolicy GetUploadPolicy(EDriveFolderKind folderKind)
    {
        return GetUploadPolicy(folderKind, EFileUploadPolicyContext.RegularFolder);
    }

    public FileUploadPolicy GetUploadPolicy(EDriveFolderKind folderKind, EFileUploadPolicyContext uploadPolicyContext)
    {
        if (uploadPolicyContext == EFileUploadPolicyContext.MinutesFolder)
        {
            return new FileUploadPolicy(MINUTES_EXTENSIONS, mOptions.MaximumUploadBytes);
        }

        return new FileUploadPolicy(MATERIAL_EXTENSIONS, mOptions.MaximumUploadBytes);
    }

    public bool CanPreviewInSite(DriveFileMetadata fileMetadata)
    {
        if (fileMetadata.MimeType == "application/pdf")
        {
            return true;
        }

        string extension = Path.GetExtension(fileMetadata.Name).ToLowerInvariant();
        return extension == ".pdf";
    }

    public FileContentType GetContentType(DriveItemName fileName)
    {
        string extension = Path.GetExtension(fileName.GetRequiredValue()).ToLowerInvariant();
        switch (extension)
        {
            case ".pdf":
                return FileContentType.CreateOptional("application/pdf");
            case ".docx":
                return FileContentType.CreateOptional("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            case ".xlsx":
                return FileContentType.CreateOptional("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            case ".pptx":
                return FileContentType.CreateOptional("application/vnd.openxmlformats-officedocument.presentationml.presentation");
            case ".png":
                return FileContentType.CreateOptional("image/png");
            case ".jpg":
                return FileContentType.CreateOptional("image/jpeg");
            case ".jpeg":
                return FileContentType.CreateOptional("image/jpeg");
            case ".zip":
                return FileContentType.CreateOptional("application/zip");
            case ".txt":
                return FileContentType.CreateOptional("text/plain");
            case ".md":
                return FileContentType.CreateOptional("text/markdown");
            default:
                return FileContentType.Default;
        }
    }
}
