# Organization Hub

Organization Hub is an internal operations workspace for teams that need documents, schedules, tasks, approvals, and administrator-defined permissions in one web application.

It is designed for one organization per deployment. A system administrator configures the organization from the admin pages: team names, team-specific positions, permission scopes, Google Drive folders, calendars, task lists, users, menus, and approval workflows.

## What It Provides

- Configurable teams and team-specific positions
- Permission grants for documents, schedules, tasks, approvals, users, menus, and system settings
- Google Drive folder mapping for common, management, team, responsible, public, and approved materials
- Google Calendar and Tasks mapping for organization-wide and team-specific work
- Approval workflows configured by requester team, requester position, step, approver rule, and final approval rule
- Server-side permission checks for direct file, calendar, task, and approval requests

## First Deployment

1. Set the required environment variables for the initial system administrator and Google OAuth.
2. Start the application behind HTTPS.
3. Sign in with the initial system administrator account.
4. Connect the operations Google account.
5. Create teams, positions, permission grants, users, folder mappings, calendars, task lists, and approval workflows.
6. Sign in with test users from several positions and confirm that visible menus and allowed actions match the configured permissions.

See [docs/index.md](docs/index.md) for deployment, operation, Google integration, permission, security, and maintenance documentation.

## Local Verification

```powershell
dotnet build OrganizationHub.sln
dotnet test OrganizationHub.sln
```
