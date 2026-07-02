using System;
using System.Globalization;
using System.Security.Cryptography;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed class Pbkdf2PasswordHashService : IPasswordHashService
{
    private const int SALT_BYTE_COUNT = 16;
    private const int HASH_BYTE_COUNT = 32;
    private const int ITERATION_COUNT = 120000;
    private const int HASH_PART_COUNT = 4;
    private const int HASH_PREFIX_PART_INDEX = 0;
    private const int ITERATION_COUNT_PART_INDEX = 1;
    private const int SALT_PART_INDEX = 2;
    private const int HASH_PART_INDEX = 3;
    private const char HASH_PART_SEPARATOR = ':';
    private const string HASH_PREFIX = "pbkdf2-sha256";

    public PasswordHash HashPassword(PlainPassword password)
    {
        string requiredPassword = password.GetRequiredValue();
        byte[] saltBytes = new byte[SALT_BYTE_COUNT];
        RandomNumberGenerator.Fill(saltBytes);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            requiredPassword,
            saltBytes,
            ITERATION_COUNT,
            HashAlgorithmName.SHA256,
            HASH_BYTE_COUNT);
        string saltText = Convert.ToBase64String(saltBytes);
        string hashText = Convert.ToBase64String(hashBytes);
        return PasswordHash.CreateRequired(
            HASH_PREFIX
            + HASH_PART_SEPARATOR
            + ITERATION_COUNT.ToString(CultureInfo.InvariantCulture)
            + HASH_PART_SEPARATOR
            + saltText
            + HASH_PART_SEPARATOR
            + hashText);
    }

    public bool IsPasswordValid(PlainPassword password, PasswordHash passwordHash)
    {
        if (password.HasValue == false || passwordHash.HasValue == false)
        {
            return false;
        }

        string requiredPasswordHash = passwordHash.GetRequiredValue();
        string[] hashParts = requiredPasswordHash.Split(HASH_PART_SEPARATOR);
        if (hashParts.Length != HASH_PART_COUNT)
        {
            return false;
        }

        if (hashParts[HASH_PREFIX_PART_INDEX] != HASH_PREFIX)
        {
            return false;
        }

        int iterationCount;
        bool isIterationParsed = int.TryParse(
            hashParts[ITERATION_COUNT_PART_INDEX],
            CultureInfo.InvariantCulture,
            out iterationCount);
        if (isIterationParsed == false)
        {
            return false;
        }

        try
        {
            byte[] saltBytes = Convert.FromBase64String(hashParts[SALT_PART_INDEX]);
            byte[] expectedHashBytes = Convert.FromBase64String(hashParts[HASH_PART_INDEX]);
            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password.GetRequiredValue(),
                saltBytes,
                iterationCount,
                HashAlgorithmName.SHA256,
                expectedHashBytes.Length);
            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
