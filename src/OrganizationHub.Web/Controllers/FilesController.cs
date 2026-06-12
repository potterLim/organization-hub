using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Controllers;

[Authorize]
public sealed class FilesController : Controller
{
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
    [HttpGet("/files/{fileID}/download")]
    public async Task<IActionResult> DownloadAsync(string fileID)
    {
        UserSession? userSessionOrNull = UserSessionFactory.CreateOrNull(User);
        if (userSessionOrNull == null)
        {
            return Unauthorized();
        }

        bool canReadFile = await mPermissionService.CanReadFileAsync(userSessionOrNull, fileID, HttpContext.RequestAborted);
        if (canReadFile == false)
        {
            return Forbid();
        }

        FileDownloadResult downloadResult = await mDriveService.DownloadFileAsync(fileID, HttpContext.RequestAborted);
        setShortPrivateCacheHeaders();
        return File(downloadResult.ContentStream, downloadResult.ContentType, downloadResult.FileName, true);
    }

    [ActionName("Stream")]
    [HttpGet("/files/{fileID}/stream")]
    public async Task<IActionResult> StreamAsync(string fileID)
    {
        UserSession? userSessionOrNull = UserSessionFactory.CreateOrNull(User);
        if (userSessionOrNull == null)
        {
            return Unauthorized();
        }

        bool canReadFile = await mPermissionService.CanReadFileAsync(userSessionOrNull, fileID, HttpContext.RequestAborted);
        if (canReadFile == false)
        {
            return Forbid();
        }

        DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(fileID, HttpContext.RequestAborted);
        if (fileMetadataOrNull == null)
        {
            return NotFound();
        }

        bool canPreview = mFilePolicyService.CanPreviewInSite(fileMetadataOrNull.MimeType, fileMetadataOrNull.Name);
        if (canPreview == false)
        {
            return BadRequest("PDF 파일만 내부 보기로 열 수 있습니다.");
        }

        FileDownloadResult downloadResult = await mDriveService.DownloadFileAsync(fileID, HttpContext.RequestAborted);
        Response.Headers.ContentDisposition = createInlineContentDisposition(fileMetadataOrNull.Name);
        setShortPrivateCacheHeaders();
        return File(downloadResult.ContentStream, "application/pdf", true);
    }

    private void setShortPrivateCacheHeaders()
    {
        Response.Headers.CacheControl = "private, max-age=120";
        Response.Headers.Pragma = "cache";
        Response.Headers.Expires = DateTimeOffset.UtcNow.AddMinutes(2).ToString("R", CultureInfo.InvariantCulture);
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

    private static bool isAsciiHeaderSafe(string value)
    {
        foreach (char currentCharacter in value)
        {
            if (currentCharacter < 32 || currentCharacter > 126 || currentCharacter == '"' || currentCharacter == '\\' || currentCharacter == ';')
            {
                return false;
            }
        }

        return true;
    }
}
