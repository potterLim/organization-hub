using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalFilePublishRequest
{
    public required DriveItemId TargetFileId
    {
        get; init;
    }
    public required DriveFolderId TargetFolderId
    {
        get; init;
    }
    public required DriveItemName FileName
    {
        get; init;
    }
    public required FileContentType ContentType
    {
        get; init;
    }
    public required ReadOnlyMemory<byte> Content
    {
        get; init;
    }
    public required string OperationKey
    {
        get; init;
    }
    public required bool IsReplacement
    {
        get; init;
    }
}
