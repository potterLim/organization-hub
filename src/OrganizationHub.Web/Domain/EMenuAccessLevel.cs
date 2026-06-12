namespace OrganizationHub.Web.Domain;

public enum EMenuAccessLevel
{
    Authenticated,
    TeamMember,
    TeamLeader,
    ManagementManager,
    Management,
    Admin
}
