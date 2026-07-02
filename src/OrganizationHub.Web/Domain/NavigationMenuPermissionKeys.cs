using System;
using System.Collections.Generic;
using System.Linq;

namespace OrganizationHub.Web.Domain;

public static class NavigationMenuPermissionKeys
{
    private const char STORAGE_SEPARATOR = ',';

    public static string CreateStorageValue(IReadOnlyList<PermissionKey> permissionKeys)
    {
        List<PermissionKey> normalizedPermissionKeys = normalizePermissionKeys(permissionKeys);
        return string.Join(STORAGE_SEPARATOR, normalizedPermissionKeys.Select(permissionKey => permissionKey.GetRequiredValue()));
    }

    public static IReadOnlyList<PermissionKey> ParseStorageValue(string? storageValueOrNull)
    {
        if (string.IsNullOrWhiteSpace(storageValueOrNull))
        {
            return new List<PermissionKey>();
        }

        string[] permissionKeyValues = storageValueOrNull.Split(STORAGE_SEPARATOR, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return CreatePermissionKeys(permissionKeyValues);
    }

    public static IReadOnlyList<PermissionKey> CreatePermissionKeys(IEnumerable<string> permissionKeyValues)
    {
        List<PermissionKey> permissionKeys = new List<PermissionKey>();
        foreach (string permissionKeyValue in permissionKeyValues)
        {
            if (string.IsNullOrWhiteSpace(permissionKeyValue))
            {
                continue;
            }

            permissionKeys.Add(new PermissionKey(permissionKeyValue));
        }

        return normalizePermissionKeys(permissionKeys);
    }

    public static string CreateSummary(string? storageValueOrNull)
    {
        IReadOnlyList<PermissionKey> permissionKeys = ParseStorageValue(storageValueOrNull);
        if (permissionKeys.Count == 0)
        {
            return "로그인한 사용자";
        }

        Dictionary<string, PermissionDefinition> definitionsByKey = PermissionCatalog.GetDefinitions()
            .ToDictionary(definition => definition.Key);
        List<string> permissionNames = new List<string>();
        foreach (PermissionKey permissionKey in permissionKeys)
        {
            PermissionDefinition? definitionOrNull;
            if (definitionsByKey.TryGetValue(permissionKey.GetRequiredValue(), out definitionOrNull))
            {
                permissionNames.Add(definitionOrNull.DisplayName);
                continue;
            }

            permissionNames.Add("등록되지 않은 권한");
        }

        return string.Join(" 또는 ", permissionNames);
    }

    public static bool ContainsUnknownPermissionKey(IReadOnlyList<PermissionKey> permissionKeys)
    {
        HashSet<string> knownPermissionKeys = PermissionCatalog.GetDefinitions()
            .Select(definition => definition.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (PermissionKey permissionKey in permissionKeys)
        {
            if (knownPermissionKeys.Contains(permissionKey.GetRequiredValue()) == false)
            {
                return true;
            }
        }

        return false;
    }

    private static List<PermissionKey> normalizePermissionKeys(IEnumerable<PermissionKey> permissionKeys)
    {
        Dictionary<string, PermissionKey> permissionKeysByValue = new Dictionary<string, PermissionKey>(StringComparer.Ordinal);
        foreach (PermissionKey permissionKey in permissionKeys)
        {
            string permissionKeyValue = permissionKey.GetRequiredValue();
            if (permissionKeysByValue.ContainsKey(permissionKeyValue))
            {
                continue;
            }

            permissionKeysByValue.Add(permissionKeyValue, permissionKey);
        }

        return permissionKeysByValue.Values.ToList();
    }
}
