using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Domain;

public sealed class StoredCompatibilityRoleUtilityTests
{
    [Fact]
    public void TryParseClaimRoleNameAcceptsDefinedRoleName()
    {
        EStoredCompatibilityRole role;

        bool isParsed = StoredCompatibilityRoleUtility.TryParseClaimRoleName("Admin", out role);

        Assert.True(isParsed);
        Assert.Equal(EStoredCompatibilityRole.Admin, role);
    }

    [Fact]
    public void TryParseClaimRoleNameRejectsUndefinedNumericRole()
    {
        EStoredCompatibilityRole role;

        bool isParsed = StoredCompatibilityRoleUtility.TryParseClaimRoleName("999", out role);

        Assert.False(isParsed);
        Assert.Equal(EStoredCompatibilityRole.TeamUser, role);
    }
}
