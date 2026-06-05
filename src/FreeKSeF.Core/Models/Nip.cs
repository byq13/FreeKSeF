namespace FreeKSeF.Core.Models;

/// <summary>
/// Obsluga numeru NIP. NIP bywa wpisywany z myslnikami, spacjami, prefiksem "PL"
/// czy innymi bialymi znakami - interesuja nas wylacznie cyfry.
/// </summary>
public static class Nip
{
    // Wagi do sumy kontrolnej NIP (pierwszych 9 cyfr).
    private static readonly int[] Wagi = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };

    /// <summary>Zwraca same cyfry z podanego tekstu (usuwa myslniki, spacje, prefiksy itp.).</summary>
    public static string Normalizuj(string? tekst)
    {
        if (string.IsNullOrEmpty(tekst)) return string.Empty;
        Span<char> bufor = tekst.Length <= 64 ? stackalloc char[tekst.Length] : new char[tekst.Length];
        var n = 0;
        foreach (var c in tekst)
            if (c is >= '0' and <= '9')
                bufor[n++] = c;
        return new string(bufor[..n]);
    }

    /// <summary>True, gdy znormalizowany NIP ma 10 cyfr i poprawna sume kontrolna.</summary>
    public static bool Waliduj(string? tekst)
    {
        var nip = Normalizuj(tekst);
        if (nip.Length != 10) return false;
        if (nip.All(c => c == nip[0])) return false; // np. 0000000000

        var suma = 0;
        for (var i = 0; i < 9; i++)
            suma += (nip[i] - '0') * Wagi[i];

        var kontrolna = suma % 11;
        if (kontrolna == 10) return false; // NIP z taka cyfra kontrolna nie istnieje
        return kontrolna == nip[9] - '0';
    }
}
