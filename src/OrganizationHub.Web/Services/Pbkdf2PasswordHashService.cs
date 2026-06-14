using System;
using System.Security.Cryptography;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed class Pbkdf2PasswordHashService : IPasswordHashService
{
    private const int SALT_BYTE_COUNT = 16;
    private const int HASH_BYTE_COUNT = 32;
    private const int ITERATION_COUNT = 120000;
    private const string HASH_PREFIX = "pbkdf2-sha256";

    public PasswordHash HashPassword(PlainPassword password)
    {
        string requiredPassword = password.GetRequiredValue();
        byte[] saltBytes = new byte[SALT_BYTE_COUNT];
        RandomNumberGenerator.Fill(saltBytes);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(requiredPassword, saltBytes, ITERATION_COUNT, HashAlgorithmName.SHA256, HASH_BYTE_COUNT);
        string saltText = Convert.ToBase64String(saltBytes);
        string hashText = Convert.ToBase64String(hashBytes);
        return PasswordHash.CreateRequired(HASH_PREFIX + ":" + ITERATION_COUNT.ToString() + ":" + saltText + ":" + hashText);
    }

    public bool VerifyPassword(PlainPassword password, PasswordHash passwordHash)
    {
        if (password.HasValue == false || passwordHash.HasValue == false)
        {
            return false;
        }

        string requiredPasswordHash = passwordHash.GetRequiredValue();
        string[] hashParts = requiredPasswordHash.Split(':');
        if (hashParts.Length != 4)
        {
            return false;
        }

        if (hashParts[0] != HASH_PREFIX)
        {
            return false;
        }

        int iterationCount;
        bool isIterationParsed = int.TryParse(hashParts[1], out iterationCount);
        if (isIterationParsed == false)
        {
            return false;
        }

        try
        {
            byte[] saltBytes = Convert.FromBase64String(hashParts[2]);
            byte[] expectedHashBytes = Convert.FromBase64String(hashParts[3]);
            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(password.GetRequiredValue(), saltBytes, iterationCount, HashAlgorithmName.SHA256, expectedHashBytes.Length);
            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
