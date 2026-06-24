# FreeKSeF — Szybka Faktura KSeF

Darmowa, prosta aplikacja desktopowa (Windows, .NET/WPF) do faktur **KSeF** dla
jednoosobowych firm usługowych (JDG). Powstała, bo każdy wygodny interfejs do
faktur (Aplikacja Podatnika, drogie chmury programów księgowych) jest płatny lub
okrojony — np. **Aplikacja Podatnika pokazuje historię tylko 30 dni wstecz**, więc
nie pobierzesz starszej faktury zakupu. A samo **API KSeF jest darmowe**.

## Co potrafi

- ✍️ **Wystawianie faktur** sprzedaży do **bufora (robocze)** i zapis lokalny (SQLite).
- 🧾 **Generowanie XML w formacie FA(3)** (schemat obowiązujący od 1.02.2026),
  walidowanego względem **oficjalnego XSD Ministerstwa Finansów**.
- 📑 **Osobne listy faktur sprzedaży i zakupu** (zakładki).
- 👁️ **Podgląd faktury jako PDF** w oknie (WebView2) + **zapis/eksport PDF**.
- 💾 **Eksport XML do pliku** — do ręcznego wgrania w Aplikacji Podatnika (gov.pl).
- 📤 **Wysyłka do KSeF** — tylko na wyraźne polecenie z **potwierdzeniem** + odbiór **UPO**.
- 📥 **Import faktur zakupu z KSeF po dowolnym zakresie dat** — bez limitu 30 dni,
  z **oszczędzaniem limitu** 64 zapytań/h (pomija faktury już pobrane).
- 🏢 **Obsługa wielu firm** — każda z własnymi fakturami, kontrahentami, tokenem i środowiskiem;
  przełącznik aktywnej firmy u góry okna.
- 🧳 **Przenośność** — baza `freeksef.db` leży **obok pliku exe** (pendrive), bez plików
  konfiguracyjnych; wszystkie ustawienia trzymane w bazie.
- 💱 **Faktury w walucie obcej** (EUR, USD…) z kursem **NBP** (własna tabela kursów, kurs z dnia
  poprzedniego, edytowalny); VAT przeliczany na PLN zgodnie z FA(3).
- 🌍 **Kontrahenci z UE i spoza UE** (kod kraju + NrVatUE/NrID), adres zagraniczny.
- 📦 **Baza produktów/usług** — szybkie wstawianie powtarzalnych pozycji.
- 🔢 **Konfigurowalna numeracja** — własny szablon numeru ({NR}/{MM}/{RRRR}) + reset miesięczny/roczny.

Planowane dalej: magazyn, pobieranie danych po NIP (GUS), korekty,
logowanie podpisem kwalifikowanym (XAdES).

## Architektura

| Projekt | Framework | Rola |
|---|---|---|
| `FreeKSeF.Core` | `net10.0` | Model domenowy, **generacja i walidacja FA(3)** (klasy z XSD, mapper, serializer, walidator). |
| `FreeKSeF.Data` | `net10.0` | EF Core + **SQLite**, encje, migracje, mapowanie encja↔model. |
| `FreeKSeF.Ksef` | `net10.0` | Integracja z KSeF (`IKsefGateway`) — oparta o oficjalny **KSeF.Client** (MF). |
| `FreeKSeF.Pdf`  | `net10.0` | Generowanie PDF faktury (**PDFsharp/MigraDoc**, MIT) z osadzonym fontem DejaVu. |
| `FreeKSeF.App`  | `net10.0-windows` | Interfejs **WPF** (MVVM), podgląd PDF przez **WebView2**. |
| `FreeKSeF.Tests`| `net10.0` | Testy: walidacja FA(3) z XSD, warstwa danych, generowanie PDF. |

Schematy XSD FA(3) (`schemat_FA(3)_v1-0E.xsd` + zależne typy MF) są osadzone w
`FreeKSeF.Core/Schemas`, dzięki czemu walidacja działa offline.

## Status

✅ Generacja i walidacja FA(3) (XML przechodzi oficjalny XSD)
✅ Warstwa danych SQLite + migracje + import zakupu (z oszczędzaniem limitu KSeF)
✅ Realna integracja KSeF na oficjalnym `KSeF.Client` 2.6.0
✅ Interfejs WPF: listy sprzedaży/zakupu, podgląd/eksport PDF, eksport XML, bufor + wysyłka z potwierdzeniem

## Wymagania

- .NET SDK 10 (desktop) — do budowy WPF na Windows (lub gotowy `FreeKSeF.exe` z CI).
- **WebView2 Runtime** — do podglądu PDF; jest **wbudowany w Windows 11** (na starszych
  systemach instaluje się z Edge / Evergreen Runtime).
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
export NUGET_GH_USER="twoj-login-github"
export NUGET_GH_PAT="ghp_xxx"
```
```powershell
# Windows PowerShell
setx NUGET_GH_USER "twoj-login-github"
setx NUGET_GH_PAT  "ghp_xxx"
```

Źródło pakietu jest już skonfigurowane w `nuget.config`. W CI dodaj **sekret repo
`NUGET_GH_PAT`** (Settings → Secrets and variables → Actions). Uwaga: nazwa sekretu
**nie może** zaczynać się od `GITHUB_` — ten prefiks jest zarezerwowany przez GitHuba.

## Licencja

[MIT](LICENSE). Wykorzystuje oficjalne, otwarte komponenty Ministerstwa Finansów
([ksef-client-csharp](https://github.com/CIRFMF/ksef-client-csharp),
schematy [ksef-docs](https://github.com/CIRFMF/ksef-docs)).
