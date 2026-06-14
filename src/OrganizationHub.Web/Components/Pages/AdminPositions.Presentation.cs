using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private string getAllowedPermissionCountText()
    {
        if (getSelectedPositionOrNull() == null)
        {
            return "직책 선택 필요";
        }

        return mPermissionFormModel.PermissionGrants.Count(grant => grant.IsAllowed).ToString(CultureInfo.InvariantCulture) + "개 허용";
    }

    private IReadOnlyList<string> getPermissionCategories()
    {
        return mPermissionDefinitions.Select(definition => definition.Category).Distinct().OrderBy(category => category).ToList();
    }

    private IReadOnlyList<string> getVisiblePermissionCategories()
    {
        List<string> categories = new List<string>();
        foreach (string category in getPermissionCategories())
        {
            IReadOnlyList<PermissionDefinition> visibleDefinitions = getVisiblePermissionDefinitions(category);
            if (visibleDefinitions.Count > 0)
            {
                categories.Add(category);
            }
        }

        return categories;
    }

    private void ensurePermissionCategorySelection()
    {
        IReadOnlyList<string> categories = getPermissionCategories();
        if (categories.Count == 0)
        {
            mSelectedPermissionCategory = ALL_PERMISSION_CATEGORY;
            return;
        }

        bool hasSelectedCategory = mSelectedPermissionCategory == ALL_PERMISSION_CATEGORY || categories.Contains(mSelectedPermissionCategory);
        if (string.IsNullOrWhiteSpace(mSelectedPermissionCategory) || hasSelectedCategory == false)
        {
            mSelectedPermissionCategory = categories[0];
        }
    }

    private IReadOnlyList<PermissionDefinition> getVisiblePermissionDefinitions(string category)
    {
        IEnumerable<PermissionDefinition> definitions = mPermissionDefinitions.Where(definition => definition.Category == category);
        if (mSelectedPermissionCategory != ALL_PERMISSION_CATEGORY)
        {
            definitions = definitions.Where(definition => definition.Category == mSelectedPermissionCategory);
        }

        if (mShowOnlyAllowedPermissions)
        {
            definitions = definitions.Where(definition => getPermissionGrantForm(definition.Key).IsAllowed);
        }

        return definitions.OrderBy(definition => definition.DisplayName).ToList();
    }

    private string getPositionOptionText(OrganizationPosition position)
    {
        if (position.IsActive)
        {
            return position.Name;
        }

        return position.Name + " (비활성)";
    }

    private string getTeamName(OrganizationPosition position)
    {
        if (position.Team == null)
        {
            return "-";
        }

        return position.Team.DisplayName;
    }
}
