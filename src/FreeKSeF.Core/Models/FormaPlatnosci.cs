namespace FreeKSeF.Core.Models;

/// <summary>
/// Forma platnosci wg slownika FA(3) (TFormaPlatnosci). Kod = wartosc w XML.
/// </summary>
public enum FormaPlatnosci
{
    Gotowka = 1,
    Karta = 2,
    Bon = 3,
    Czek = 4,
    Kredyt = 5,
    Przelew = 6,
    Mobilna = 7,
}
