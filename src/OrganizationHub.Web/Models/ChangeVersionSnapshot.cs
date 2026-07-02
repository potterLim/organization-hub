using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ChangeVersionSnapshot
{
    private const long INITIAL_VERSION = 0;

    public Dictionary<ChangeVersionKey, long> Versions { get; set; } = new Dictionary<ChangeVersionKey, long>();

    public long GetVersion(ChangeVersionKey key)
    {
        long version;
        if (Versions.TryGetValue(key, out version))
        {
            return version;
        }

        return INITIAL_VERSION;
    }
}
