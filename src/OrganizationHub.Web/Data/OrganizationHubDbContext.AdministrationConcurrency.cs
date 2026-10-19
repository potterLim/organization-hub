using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDbContext
{
    private void prepareAdministrativeRevisions()
    {
        ChangeTracker.DetectChanges();
        HashSet<int> changedPositionIds = new HashSet<int>();
        foreach (EntityEntry<PositionPermissionGrant> grantEntry in ChangeTracker.Entries<PositionPermissionGrant>().ToList())
        {
            if (grantEntry.State != EntityState.Unchanged && grantEntry.State != EntityState.Detached && grantEntry.Entity.PositionId > 0)
            {
                changedPositionIds.Add(grantEntry.Entity.PositionId);
            }
        }

        foreach (int positionId in changedPositionIds)
        {
            OrganizationPosition? positionOrNull = Positions.Find(positionId);
            if (positionOrNull != null)
            {
                EntityEntry<OrganizationPosition> positionEntry = Entry(positionOrNull);
                if (positionEntry.State != EntityState.Added && positionEntry.State != EntityState.Deleted)
                {
                    positionEntry.Property(position => position.Revision).IsModified = true;
                }
            }
        }

        foreach (EntityEntry<OrganizationPosition> positionEntry in ChangeTracker.Entries<OrganizationPosition>())
        {
            if (positionEntry.State == EntityState.Modified)
            {
                positionEntry.Entity.Revision = positionEntry.Property(position => position.Revision).OriginalValue + 1;
            }
        }

        foreach (EntityEntry<UserAccount> userEntry in ChangeTracker.Entries<UserAccount>())
        {
            if (userEntry.State == EntityState.Modified && userEntry.Property(user => user.PasswordHash).IsModified)
            {
                userEntry.Entity.AuthenticationVersion = userEntry.Property(user => user.AuthenticationVersion).OriginalValue + 1;
            }

            if (userEntry.State == EntityState.Modified && userEntry.Properties.Any(isAdministrativeUserChange))
            {
                userEntry.Entity.Revision = userEntry.Property(user => user.Revision).OriginalValue + 1;
            }
        }
    }

    private static bool isAdministrativeUserChange(PropertyEntry property)
    {
        return property.IsModified && property.Metadata.Name != nameof(UserAccount.LastLoginAt) && property.Metadata.Name != nameof(UserAccount.Revision);
    }
}
