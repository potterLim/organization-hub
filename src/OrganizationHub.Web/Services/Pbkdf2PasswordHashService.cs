using System;
using System.Security.Cryptography;

namespace OrganizationHub.Web.Services;

public sealed class Pbkdf2PasswordHashService : IPasswordHashService
{
    private const int SALT_BYTE_COUNT = 16;
    private const int HASH_BYTE_COUNT = 32;
    private const int ITERATION_COUNT = 120000;
    private const string HASH_PREFIX = "pbkdf2-sha256";

    public string HashPassword(string password)
    {
        byte[] saltBytes = new byte[SALT_BYTE_COUNT];
        RandomNumberGenerator.Fill(saltBytes);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, ITERATION_COUNT, HashAlgorithmName.SHA256, HASH_BYTE_COUNT);
        string saltText = Convert.ToBase64String(saltBytes);
        string hashText = Convert.ToBase64String(hashBytes);
        return HASH_PREFIX + ":" + ITERATION_COUNT.ToString() + ":" + saltText + ":" + hashText;
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        string[] hashParts = passwordHash.Split(':');
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
            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterationCount, HashAlgorithmName.SHA256, expectedHashBytes.Length);
            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

