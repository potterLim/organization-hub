using System.IO;

namespace OrganizationHub.Web.Services;

internal sealed class FileSizeLimitExceededException : IOException
{
    public FileSizeLimitExceededException() : base("The file exceeds the configured byte limit.")
    {
    }
}
