using System.IO;

namespace OrganizationHub.Web.Models;

public sealed class FileDownloadResult
{
    public Stream ContentStream
    {
        get; private init;
    }
    public string FileName { get; private init; } = string.Empty;
    public string ContentType { get; private init; } = string.Empty;

    public FileDownloadResult(Stream contentStream, string fileName, string contentType)
    {
        ContentStream = contentStream;
        FileName = fileName;
        ContentType = contentType;
    }
}

