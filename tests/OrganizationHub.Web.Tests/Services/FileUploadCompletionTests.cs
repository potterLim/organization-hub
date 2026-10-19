using System;
using System.IO;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class FileUploadCompletionTests
{
    [Fact]
    public async Task AuditFailureRetainsConfirmedFileWithoutRepeatingRemoteUploadAsync()
    {
        int uploadCount = 0;
        DriveFileItem file = new DriveFileItem();
        file.Id = "stored-file";
        Exception? loggedExceptionOrNull = null;
        FileUploadCompletion result = await FileUploadCompletion.ExecuteAsync(uploadAsync, recordAuditAsync, exception => loggedExceptionOrNull = exception);
        Assert.Same(file, result.File);
        Assert.False(result.IsAuditRecorded);
        Assert.Equal(1, uploadCount);
        Assert.IsType<IOException>(loggedExceptionOrNull);

        Task<DriveFileItem> uploadAsync()
        {
            uploadCount++;
            return Task.FromResult(file);
        }

        Task recordAuditAsync(DriveFileItem uploadedFile)
        {
            Assert.Same(file, uploadedFile);
            throw new IOException("Audit database is unavailable.");
        }
    }

    [Fact]
    public async Task UnconfirmedRemoteFailureDoesNotRecordAnAuditOrClaimSuccessAsync()
    {
        bool isAuditCalled = false;
        await Assert.ThrowsAsync<IOException>(() => FileUploadCompletion.ExecuteAsync(uploadAsync, recordAuditAsync, exception => Assert.Fail(exception.Message)));
        Assert.False(isAuditCalled);

        Task<DriveFileItem> uploadAsync()
        {
            throw new IOException("The connection closed after sending the file.");
        }

        Task recordAuditAsync(DriveFileItem file)
        {
            isAuditCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SuccessfulUploadRecordsTheReturnedFileAsync()
    {
        DriveFileItem file = new DriveFileItem();
        FileUploadCompletion result = await FileUploadCompletion.ExecuteAsync(() => Task.FromResult(file), uploadedFile => Task.CompletedTask, exception => Assert.Fail(exception.Message));
        Assert.True(result.IsAuditRecorded);
        Assert.Same(file, result.File);
    }
}
