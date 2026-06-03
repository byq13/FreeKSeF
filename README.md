# FreeKSeF — Szybka Faktura KSeF

Darmowa, prosta aplikacja desktopowa (Windows, .NET/WPF) do faktur **KSeF** dla
jednoosobowych firm usługowych (JDG). Powstała, bo każdy wygodny interfejs do
faktur (Aplikacja Podatnika, drogie chmury programów księgowych) jest płatny lub
okrojony — np. **Aplikacja Podatnika pokazuje historię tylko 30 dni wstecz**, więc
nie pobierzesz starszej faktury zakupu. A samo **API KSeF jest darmowe**.

## Co potrafi (zakres MVP)

- ✍️ **Wystawianie faktur** sprzedaży i zapis lokalny (SQLite).
- 🧾 **Generowanie XML w formacie FA(3)** (schemat obowiązujący od 1.02.2026),
  walidowanego względem **oficjalnego XSD Ministerstwa Finansów**.
- 📤 **Wysyłka do KSeF** przez API (sesja interaktywna) + odbiór **UPO**.
- 💾 **Eksport XML do pliku** — do ręcznego wgrania w Aplikacji Podatnika.
- 📥 **Import faktur zakupu z KSeF po dowolnym zakresie dat** — bez limitu 30 dni.
- 🔎 Podgląd faktur wystawionych i zaimportowanych.

Planowane dalej: magazyn, pobieranie danych po NIP (GUS), generowanie PDF, korekty,
logowanie podpisem kwalifikowanym (XAdES).

## Architektura

| Projekt | Framework | Rola |
|---|---|---|
| `FreeKSeF.Core` | `net8.0` | Model domenowy, **generacja i walidacja FA(3)** (klasy z XSD, mapper, serializer, walidator). |
| `FreeKSeF.Data` | `net8.0` | EF Core + **SQLite**, encje, migracje, mapowanie encja↔model. |
| `FreeKSeF.Ksef` | `net8.0` | Integracja z KSeF (`IKsefGateway`) — oparta o oficjalny **KSeF.Client** (MF). |
| `FreeKSeF.App`  | `net8.0-windows` | Interfejs **WPF** (MVVM). |
| `FreeKSeF.Tests`| `net8.0` | Testy: walidacja FA(3) z XSD, warstwa danych. |

Schematy XSD FA(3) (`schemat_FA(3)_v1-0E.xsd` + zależne typy MF) są osadzone w
`FreeKSeF.Core/Schemas`, dzięki czemu walidacja działa offline.

## Status

✅ Generacja i walidacja FA(3) (XML przechodzi oficjalny XSD)
✅ Warstwa danych SQLite + migracje + import zakupu z XML
✅ Kontrakt integracji KSeF (`IKsefGateway`)
⏳ Realna implementacja KSeF na `KSeF.Client` (wymaga tokenu PAT — patrz niżej)
⏳ Interfejs WPF

## Wymagania

- .NET SDK 8 (desktop) — do budowy WPF na Windows.
- Konto i token **KSeF** (na start środowisko testowe).

## Budowanie

```bash
dotnet restore FreeKSeF.sln
dotnet build   FreeKSeF.sln -c Release
dotnet test    FreeKSeF.sln -c Release
```

> Biblioteki i testy budują się na dowolnym OS. Aplikacja WPF (`FreeKSeF.App`)
> buduje się tylko na **Windows** (oraz w CI na `windows-latest`).

## Konfiguracja pakietu KSeF.Client (token PAT)

Oficjalny klient KSeF jest publikowany na **GitHub Packages** organizacji `CIRFMF`
i wymaga autoryzacji. Utwórz **Personal Access Token (classic)** z uprawnieniem
`read:packages`, a następnie ustaw zmienne środowiskowe (token NIE trafia do repo):

```bash
# Linux/macOS
export GITHUB_PACKAGES_USER="twoj-login-github"
export GITHUB_PACKAGES_PAT="ghp_xxx"
```
```powershell
# Windows PowerShell
setx GITHUB_PACKAGES_USER "twoj-login-github"
setx GITHUB_PACKAGES_PAT  "ghp_xxx"
```

Źródło pakietu jest już skonfigurowane w `nuget.config`. W CI dodaj sekret repo
`GITHUB_PACKAGES_PAT`.

## Licencja

[MIT](LICENSE). Wykorzystuje oficjalne, otwarte komponenty Ministerstwa Finansów
([ksef-client-csharp](https://github.com/CIRFMF/ksef-client-csharp),
schematy [ksef-docs](https://github.com/CIRFMF/ksef-docs)).
