using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class FileUploadPolicy
{
    public IReadOnlySet<string> AllowedExtensions { get; private init; } = new HashSet<string>();
    public long MaximumUploadBytes
    {
        get; private init;
    }

    public FileUploadPolicy(IReadOnlySet<string> allowedExtensions, long maximumUploadBytes)
    {
        AllowedExtensions = allowedExtensions;
        MaximumUploadBytes = maximumUploadBytes;
    }
}

