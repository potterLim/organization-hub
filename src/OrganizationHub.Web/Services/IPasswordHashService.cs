using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public interface IPasswordHashService
{
    PasswordHash HashPassword(PlainPassword password);
    bool VerifyPassword(PlainPassword password, PasswordHash passwordHash);
}
