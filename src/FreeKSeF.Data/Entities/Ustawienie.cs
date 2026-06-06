namespace FreeKSeF.Data.Entities;

/// <summary>
/// Proste ustawienie aplikacji w formie klucz-wartosc (np. wybrany filtr,
/// uklad kolumn tabeli). Pozwala zapamietywac preferencje miedzy uruchomieniami.
/// </summary>
public class Ustawienie
{
    public string Klucz { get; set; } = string.Empty;
    public string Wartosc { get; set; } = string.Empty;
}
