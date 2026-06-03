using System.Security.Cryptography;
using System.Text;

namespace FreeKSeF.App.Services;

/// <summary>
/// Szyfrowanie sekretow (token KSeF) przy uzyciu Windows DPAPI - dane sa odszyfrowywalne
/// tylko na koncie biezacego uzytkownika. Token nigdy nie jest zapisywany jawnie.
/// </summary>
public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FreeKSeF.v1");

    public static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null; // np. przeniesienie pliku na inne konto/maszyne
        }
    }
}
