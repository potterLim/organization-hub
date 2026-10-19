using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Tests.Fakes;

public sealed partial class FakeDriveService
{
    public enum EPublicationFailure
    {
        None,
        BeforeUpload,
        AfterUpload
    }

    private readonly ConcurrentDictionary<string, byte[]> mContentsById = new ConcurrentDictionary<string, byte[]>();
    private readonly ConcurrentDictionary<string, string> mPublicationKeysById = new ConcurrentDictionary<string, string>();
    private int mGeneratedIdCount;
    private int mPublicationWriteCount;
    private int mNextPublicationFailure;

    public int PublicationWriteCount => mPublicationWriteCount;
    public TaskCompletionSource<bool>? PublicationStartedOrNull
    {
        get; set;
    }
    public TaskCompletionSource<bool>? ContinuePublicationOrNull
    {
        get; set;
    }

    public void SetFileContent(string fileId, byte[] content)
    {
        mContentsById[fileId] = content.ToArray();
    }

    public void FailNextPublication(EPublicationFailure failure)
    {
        Interlocked.Exchange(ref mNextPublicationFailure, (int)failure);
    }

    public Task<DriveItemId> GenerateFileIdAsync(CancellationToken cancellationToken)
    {
        int id = Interlocked.Increment(ref mGeneratedIdCount);
        return Task.FromResult(DriveItemId.CreateRequired("approval-file-" + id));
    }

    public Task<FileDownloadResult> DownloadFileSnapshotAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        string requiredFileId = fileId.GetRequiredValue();
        DriveFileMetadata metadata = mMetadataByFileId[requiredFileId];
        byte[]? contentOrNull;
        if (mContentsById.TryGetValue(requiredFileId, out contentOrNull) == false)
        {
            contentOrNull = Array.Empty<byte>();
        }

        FileDownloadResult download = new FileDownloadResult(new MemoryStream(contentOrNull, false), DriveItemName.CreateRequired(metadata.Name), FileContentType.CreateOptional(metadata.MimeType));
        return Task.FromResult(download);
    }

    public async Task<DriveFileItem> PublishApprovalFileAsync(ApprovalFilePublishRequest request, CancellationToken cancellationToken)
    {
        if (PublicationStartedOrNull != null)
        {
            PublicationStartedOrNull.TrySetResult(true);
        }

        if (ContinuePublicationOrNull != null)
        {
            await ContinuePublicationOrNull.Task.WaitAsync(cancellationToken);
        }

        EPublicationFailure failure = (EPublicationFailure)Interlocked.Exchange(ref mNextPublicationFailure, (int)EPublicationFailure.None);
        if (failure == EPublicationFailure.BeforeUpload)
        {
            throw new IOException("Injected publication failure before upload.");
        }

        string fileId = request.TargetFileId.GetRequiredValue();
        string? operationKeyOrNull;
        if (mPublicationKeysById.TryGetValue(fileId, out operationKeyOrNull) && operationKeyOrNull == request.OperationKey)
        {
            if (mContentsById[fileId].AsSpan().SequenceEqual(request.Content.Span) == false)
            {
                throw new IOException("The fake publication content was changed.");
            }

            return createDriveFileItem(mMetadataByFileId[fileId]);
        }

        Interlocked.Increment(ref mPublicationWriteCount);
        AddFile(fileId, request.FileName.GetRequiredValue(), request.ContentType.GetRequiredValue(), request.TargetFolderId.GetRequiredValue(), "official-root");
        SetFileContent(fileId, request.Content.ToArray());
        mPublicationKeysById[fileId] = request.OperationKey;
        if (failure == EPublicationFailure.AfterUpload)
        {
            throw new IOException("Injected lost response after upload.");
        }

        return createDriveFileItem(mMetadataByFileId[fileId]);
    }
}
