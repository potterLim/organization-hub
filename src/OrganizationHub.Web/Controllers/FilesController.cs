using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class FilesController : Controller
{
    private const int MINIMUM_SAFE_ASCII_CODE = 32;
    private const int MAXIMUM_SAFE_ASCII_CODE = 126;

    private readonly IDriveService mDriveService;
    private readonly IPermissionService mPermissionService;
    private readonly IFilePolicyService mFilePolicyService;

    public FilesController(IDriveService driveService, IPermissionService permissionService, IFilePolicyService filePolicyService)
    {
        mDriveService = driveService;
        mPermissionService = permissionService;
        mFilePolicyService = filePolicyService;
    }

    [ActionName("Download")]
    [HttpGet("/files/{fileId}/download")]
    public async Task<IActionResult> DownloadAsync(string fileId)
    {
        UserSession? userSessionOrNull = UserSessionFactory.CreateOrNull(User);
        if (userSessionOrNull == null)
        {
            return Unauthorized();
        }

        DriveItemId requestedFileId = DriveItemId.CreateOptional(fileId);
        bool canReadFile = await mPermissionService.CanReadFileAsync(userSessionOrNull, requestedFileId, HttpContext.RequestAborted);
        if (canReadFile == false)
        {
            return Forbid();
        }

        FileDownloadResult downloadResult = await mDriveService.DownloadFileAsync(requestedFileId, HttpContext.RequestAborted);
        return File(downloadResult.ContentStream, downloadResult.ContentType, downloadResult.FileName, true);
    }

    [ActionName("Stream")]
    [HttpGet("/files/{fileId}/stream")]
    public async Task<IActionResult> StreamAsync(string fileId)
    {
        UserSession? userSessionOrNull = UserSessionFactory.CreateOrNull(User);
        if (userSessionOrNull == null)
        {
            return Unauthorized();
        }

        DriveItemId requestedFileId = DriveItemId.CreateOptional(fileId);
        bool canReadFile = await mPermissionService.CanReadFileAsync(userSessionOrNull, requestedFileId, HttpContext.RequestAborted);
        if (canReadFile == false)
        {
            return Forbid();
        }

        DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(requestedFileId, HttpContext.RequestAborted);
        if (fileMetadataOrNull == null)
        {
            return NotFound();
        }

        bool canPreview = mFilePolicyService.CanPreviewInSite(fileMetadataOrNull);
        if (canPreview == false)
        {
            return BadRequest("PDF 파일만 내부 보기로 열 수 있습니다.");
        }

        FileDownloadResult downloadResult = await mDriveService.DownloadFileAsync(requestedFileId, HttpContext.RequestAborted);
        Response.Headers.ContentDisposition = createInlineContentDisposition(fileMetadataOrNull.Name);
        return File(downloadResult.ContentStream, GoogleMimeTypes.PDF, true);
    }

    private static string createInlineContentDisposition(string fileName)
    {
        string fallbackFileName = createAsciiFallbackFileName(fileName);
        string encodedFileName = Uri.EscapeDataString(fileName);
        return "inline; filename=\"" + fallbackFileName + "\"; filename*=UTF-8''" + encodedFileName;
    }

    private static string createAsciiFallbackFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || isAsciiHeaderSafe(extension) == false)
        {
            extension = ".pdf";
        }

        return "document" + extension;
    }

    private static bool isAsciiHeaderSafe(string headerValue)
    {
        foreach (char currentCharacter in headerValue)
        {
            if (currentCharacter < MINIMUM_SAFE_ASCII_CODE || currentCharacter > MAXIMUM_SAFE_ASCII_CODE || currentCharacter == '"' || currentCharacter == '\\' || currentCharacter == ';')
            {
                return false;
            }
        }

        return true;
    }
}
