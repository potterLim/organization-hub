using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private sealed class ValidatedApprovalSubmission
    {
        public required UserSession UserSession
        {
            get; init;
        }

        public required UserAccount? RequesterUserOrNull
        {
            get; init;
        }

        public required ApprovalRequestCommand Command
        {
            get; init;
        }

        public required ApprovalCreateContext CreateContext
        {
            get; init;
        }

        public required DriveFolderReference SourceFolder
        {
            get; init;
        }

        public required DriveFileMetadata FileMetadata
        {
            get; init;
        }

        public required DriveFileMetadata DestinationFolderMetadata
        {
            get; init;
        }
    }
}
