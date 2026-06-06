using FreeKSeF.Data.Entities;

namespace FreeKSeF.App.Services;

/// <summary>Zapamietywanie prostych preferencji aplikacji (klucz-wartosc) w bazie.</summary>
public static class Ustawienia
{
    public static string? Pobierz(string klucz)
    {
        using var db = AppServices.Db();
        return db.Ustawienia.Find(klucz)?.Wartosc;
    }

    public static void Zapisz(string klucz, string wartosc)
    {
        using var db = AppServices.Db();
        var u = db.Ustawienia.Find(klucz);
        if (u is null)
            db.Ustawienia.Add(new Ustawienie { Klucz = klucz, Wartosc = wartosc });
        else
            u.Wartosc = wartosc;
        db.SaveChanges();
    }
}
