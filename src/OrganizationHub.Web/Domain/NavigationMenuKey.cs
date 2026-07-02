using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct NavigationMenuKey
{
    public const string DASHBOARD = "Dashboard";
    public const string CALENDAR = "Calendar";
    public const string TODOS = "Todos";
    public const string WORK_GUIDELINES = "WorkGuidelines";
    public const string WORK_RESOURCES = "WorkResources";
    public const string MATERIALS = "Materials";
    public const string RESPONSIBLE_RESOURCES = "ResponsibleResources";
    public const string MANAGEMENT_FILES = "ManagementFiles";
    public const string COMMON = "Common";
    public const string PUBLIC = "Public";
    public const string UPLOAD = "Upload";
    public const string APPROVAL_REQUEST = "ApprovalRequest";
    public const string APPROVAL_INBOX = "ApprovalInbox";
    public const string APPROVAL_HISTORY = "ApprovalHistory";
    public const string APPROVED_FILES = "ApprovedFiles";
    public const string ADMIN_USERS = "AdminUsers";
    public const string ADMIN_TEAMS = "AdminTeams";
    public const string ADMIN_POSITIONS = "AdminPositions";
    public const string ADMIN_APPROVAL_WORKFLOWS = "AdminApprovalWorkflows";
    public const string ADMIN_SETTINGS = "AdminSettings";
    public const string ADMIN_MENUS = "AdminMenus";
    public const string ADMIN_GOOGLE = "AdminGoogle";
    public const string ADMIN_CALENDAR_AUDIT = "AdminCalendarAudit";
    public const string MINUTES = "Minutes";
    public const string ARCHIVE = "Archive";
    public const string RULES = "Rules";
    public const string ON_SITE = "OnSite";
    public const string LEADERS = "Leaders";

    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return string.Empty;
            }

            return mValueOrNull;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static NavigationMenuKey Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static NavigationMenuKey ManagementFiles
    {
        get
        {
            return CreateRequired(MANAGEMENT_FILES);
        }
    }

    private NavigationMenuKey(string menuKey)
    {
        mValueOrNull = menuKey;
    }

    public static NavigationMenuKey CreateOptional(string? menuKeyOrNull)
    {
        if (string.IsNullOrWhiteSpace(menuKeyOrNull))
        {
            return new NavigationMenuKey(string.Empty);
        }

        return new NavigationMenuKey(menuKeyOrNull.Trim());
    }

    public static NavigationMenuKey CreateRequired(string? menuKeyOrNull)
    {
        NavigationMenuKey menuKey = CreateOptional(menuKeyOrNull);
        if (menuKey.HasValue == false)
        {
            throw new ArgumentException("Navigation menu key is required.", nameof(menuKeyOrNull));
        }

        return menuKey;
    }

    public bool Is(NavigationMenuKey otherMenuKey)
    {
        return string.Equals(Value, otherMenuKey.GetOptionalValue(), StringComparison.Ordinal);
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Navigation menu key is required.");
        }

        return Value;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
