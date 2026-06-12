using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class ChangeVersionSnapshot
{
    public Dictionary<string, long> Versions { get; set; } = new Dictionary<string, long>();

    public long GetVersion(string key)
    {
        long version;
        if (Versions.TryGetValue(key, out version))
        {
            return version;
        }

        return 0;
    }
}
