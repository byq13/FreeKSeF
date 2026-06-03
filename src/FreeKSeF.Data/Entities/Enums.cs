namespace FreeKSeF.Data.Entities;

/// <summary>Srodowisko KSeF, do ktorego laczy sie aplikacja.</summary>
public enum Srodowisko
{
    Test = 0,
    Demo = 1,
    Produkcja = 2,
}

/// <summary>Kierunek faktury z perspektywy uzytkownika.</summary>
public enum KierunekFaktury
{
    /// <summary>Faktura sprzedazy (wystawiona przez nas).</summary>
    Sprzedaz = 0,

    /// <summary>Faktura zakupu (zaimportowana z KSeF).</summary>
    Zakup = 1,
}

/// <summary>Status faktury w obiegu KSeF.</summary>
public enum StatusFaktury
{
    /// <summary>Zapisana lokalnie, nie wyslana.</summary>
    Robocza = 0,

    /// <summary>Wyslana do KSeF, oczekuje na potwierdzenie.</summary>
    Wyslana = 1,

    /// <summary>Przyjeta przez KSeF (mamy numer KSeF / UPO).</summary>
    Przyjeta = 2,

    /// <summary>Odrzucona lub blad wysylki.</summary>
    Blad = 3,

    /// <summary>Zaimportowana z KSeF (faktura zakupu).</summary>
    Zaimportowana = 4,
}
