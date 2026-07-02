using System.Collections.Generic;

namespace OrganizationHub.Web.Services;

internal static class OrderUpdateRequestValidation
{
    internal static bool hasDuplicateValues<TValue>(IReadOnlyList<TValue> orderedValues)
        where TValue : notnull
    {
        HashSet<TValue> seenValues = new HashSet<TValue>();
        foreach (TValue orderedValue in orderedValues)
        {
            if (seenValues.Add(orderedValue) == false)
            {
                return true;
            }
        }

        return false;
    }
}
