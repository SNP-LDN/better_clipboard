using System.Security.Cryptography;
using System.Text;

namespace BetterClipboard.Services;

public sealed class EncryptionService
{
    public byte[] ProtectBytes(byte[] value)
    {
        return ProtectedData.Protect(
            value,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
    }

    public byte[] UnprotectBytes(byte[] encryptedValue)
    {
        return ProtectedData.Unprotect(
            encryptedValue,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
    }

    public string ProtectString(string value)
    {
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(encryptedBytes);
    }

    public string UnprotectString(string encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
        {
            return "";
        }

        var encryptedBytes = Convert.FromBase64String(encryptedValue);
        var plainBytes = ProtectedData.Unprotect(
            encryptedBytes,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
