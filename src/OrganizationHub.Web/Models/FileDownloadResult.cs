using System.IO;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class FileDownloadResult
{
    public Stream ContentStream
    {
        get; private init;
    }
    public string FileName { get; private init; } = string.Empty;
    public string ContentType { get; private init; } = string.Empty;

    public FileDownloadResult(Stream contentStream, DriveItemName fileName, FileContentType contentType)
    {
        ContentStream = contentStream;
        FileName = fileName.GetRequiredValue();
        ContentType = contentType.GetRequiredValue();
    }
}
