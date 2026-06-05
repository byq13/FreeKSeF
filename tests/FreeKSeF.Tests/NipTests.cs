using FreeKSeF.Core.Models;
using Xunit;

namespace FreeKSeF.Tests;

public class NipTests
{
    [Theory]
    [InlineData("525-26-74-798", "5252674798")]
    [InlineData("  525 267 47 98 ", "5252674798")]
    [InlineData("PL5252674798", "5252674798")]
    [InlineData("525\t267\n47-98", "5252674798")]
    public void Normalizuj_zostawia_same_cyfry(string wejscie, string oczekiwane)
        => Assert.Equal(oczekiwane, Nip.Normalizuj(wejscie));

    [Theory]
    [InlineData("5252674798")]   // Allegro
    [InlineData("526-000-12-46")]
    [InlineData("113-299-40-96")]
    public void Waliduj_akceptuje_poprawne(string nip)
        => Assert.True(Nip.Waliduj(nip));

    [Theory]
    [InlineData("5252674799")]   // zla suma kontrolna
    [InlineData("12345")]        // za krotki
    [InlineData("0000000000")]   // same zera
    [InlineData("")]
    [InlineData(null)]
    public void Waliduj_odrzuca_bledne(string? nip)
        => Assert.False(Nip.Waliduj(nip));
}
