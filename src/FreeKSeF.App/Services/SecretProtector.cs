using System.Security.Cryptography;
using System.Text;

namespace FreeKSeF.App.Services;

/// <summary>
/// Szyfrowanie tokena KSeF tak, by baza byla PRZENOSNA (pendrive, inny komputer):
/// <list type="bullet">
/// <item>"v2:" - AES-GCM kluczem wbudowanym w aplikacje. Samo podejrzenie pliku bazy
///   nie ujawnia tokena (pelnej ochrony nie daje, bo klucz siedzi w exe).</item>
/// <item>"v3:" - dodatkowo zaszyfrowane haslem uzytkownika (PBKDF2 + AES-GCM,
///   podwojnie: haslo owija zapis "v2:"). Bez hasla tokena NIE DA SIE odzyskac.</item>
/// <item>starszy zapis (bez prefiksu) - Windows DPAPI; dziala tylko na komputerze,
///   na ktorym go zapisano. Po udanym odczycie jest migrowany do "v2:".</item>
/// </list>
/// </summary>
public static class SecretProtector
{
    private const string PrefiksV2 = "v2:";
    private const string PrefiksV3 = "v3:";

    // Entropia starego zapisu DPAPI (tylko do odczytu/migracji).
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FreeKSeF.v1");

    // Klucz aplikacji - celowo wbudowany w exe (wymaganie: przenosnosc bez konfiguracji).
    private static readonly byte[] KluczAplikacji =
        SHA256.HashData(Encoding.UTF8.GetBytes("FreeKSeF.token.v2|baza-bez-exe-nie-wystarczy"));

    /// <summary>Szyfruje token. Z haslem = podwojnie (v3), bez hasla = kluczem aplikacji (v2).</summary>
    public static string? Protect(string? plain, string? haslo = null)
    {
        if (string.IsNullOrEmpty(plain)) return null;

        var v2 = PrefiksV2 + Zaszyfruj(Encoding.UTF8.GetBytes(plain), KluczAplikacji);
        if (string.IsNullOrEmpty(haslo)) return v2;

        var salt = RandomNumberGenerator.GetBytes(16);
        var klucz = KluczZHasla(haslo, salt);
        return PrefiksV3 + Convert.ToBase64String(salt) + "." + Zaszyfruj(Encoding.UTF8.GetBytes(v2), klucz);
    }

    /// <summary>True, gdy zapis wymaga hasla uzytkownika do odszyfrowania.</summary>
    public static bool ChronionyHaslem(string? blob)
        => blob?.StartsWith(PrefiksV3, StringComparison.Ordinal) == true;

    /// <summary>True, gdy to stary zapis DPAPI (do migracji na przenosny format).</summary>
    public static bool StaryFormatDpapi(string? blob)
        => !string.IsNullOrEmpty(blob)
           && !blob.StartsWith(PrefiksV2, StringComparison.Ordinal)
           && !blob.StartsWith(PrefiksV3, StringComparison.Ordinal);

    /// <summary>
    /// Odszyfrowuje token. Dla zapisu "v3:" wymagane jest haslo.
    /// Null = zle haslo, inny komputer (DPAPI) albo uszkodzony wpis.
    /// </summary>
    public static string? Unprotect(string? blob, string? haslo = null)
    {
        if (string.IsNullOrEmpty(blob)) return null;
        try
        {
            if (blob.StartsWith(PrefiksV3, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(haslo)) return null;
                var czesci = blob[PrefiksV3.Length..].Split('.', 2);
                if (czesci.Length != 2) return null;
                var salt = Convert.FromBase64String(czesci[0]);
                var v2 = Encoding.UTF8.GetString(Odszyfruj(czesci[1], KluczZHasla(haslo, salt)));
                return Unprotect(v2);
            }

            if (blob.StartsWith(PrefiksV2, StringComparison.Ordinal))
                return Encoding.UTF8.GetString(Odszyfruj(blob[PrefiksV2.Length..], KluczAplikacji));

            // Stary zapis DPAPI - odszyfrowywalny tylko na koncie/komputerze, na ktorym powstal.
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(blob), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    // --- AES-GCM: [12B nonce][16B tag][szyfrogram], calosc w Base64 ---

    private static string Zaszyfruj(byte[] dane, byte[] klucz)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var szyfr = new byte[dane.Length];
        using var aes = new AesGcm(klucz, 16);
        aes.Encrypt(nonce, dane, szyfr, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. szyfr]);
    }

    private static byte[] Odszyfruj(string base64, byte[] klucz)
    {
        var blob = Convert.FromBase64String(base64);
        if (blob.Length < 28) throw new CryptographicException("Za krotki zapis.");
        var dane = new byte[blob.Length - 28];
        using var aes = new AesGcm(klucz, 16);
        aes.Decrypt(blob.AsSpan(0, 12), blob.AsSpan(28), blob.AsSpan(12, 16), dane);
        return dane;
    }

    private static byte[] KluczZHasla(string haslo, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(haslo, salt, 200_000, HashAlgorithmName.SHA256, 32);
}
