# DigitalniKlubCitalaca.Tests – upute za tim (Task 2)

## Pokretanje
1. Otvoriti `DigitalniKlubCitalaca/DigitalniKlubCitalaca.sln` (test projekat je već dodan u solution).
2. Build → NuGet paketi (MSTest, Moq, EF Core InMemory, coverlet) se povuku sami.
3. Test → Test Explorer → Run All.

## Pokrivenost koda
- **VS Enterprise:** Test → Analyze Code Coverage for All Tests (koristi `coverage.runsettings`).
- **VS Community:** instalirati ekstenziju *Fine Code Coverage* i pokrenuti testove. Ili iz terminala:
  ```
  dotnet test --settings DigitalniKlubCitalaca.Tests/coverage.runsettings --collect:"XPlat Code Coverage"
  dotnet tool install -g dotnet-reportgenerator-globaltool
  reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
  ```
  Otvoriti `coverage-report/index.html` i uslikati red za svaku svoju klasu.
- Program, Views i Migrations su isključeni iz pokrivenosti (`coverage.runsettings`).

## Pravila (da svi radimo isto)
- Svaka članica ima **svoj folder**: `Lejla/`, `Berina/`, `Nejra/`, `Nedzmija/`, `Amina/` i `TestData/<Ime>/`.
- Jedna test klasa po testiranoj klasi: `<Klasa>Tests.cs`; u komentaru iznad klase upisati autoricu.
- Naziv testa: `Metoda_Scenarij_OcekivaniRezultat`, AAA (Arrange / Act / Assert), bez if-ova i petlji.
- Baza: `TestHelper.NoveOpcije()` → svaki test dobija novu InMemory bazu (testovi su nezavisni).
  Podatke ubacivati preko posebnog konteksta (`using var seed = TestHelper.NoviKontekst(opcije)`),
  a kontroleru dati novi kontekst, inače `Update()` pada zbog već praćenog entiteta.
- Kontroler: `TestHelper.PripremiKontroler(new XController(...), "korisnikId")` – postavlja `User` i mock `Url`.
- Kontroler sa `UserManager<Korisnik>`: `TestHelper.UserManagerMock(korisnik)` (null = neprijavljen).
- `catch (DbUpdateConcurrencyException) { ... throw; }` grana: `new KonkurentnostStubKontekst(opcije)`.
- Data driven: `[DynamicData(nameof(Metoda), DynamicDataSourceType.Method)]`, fajlovi u `TestData/<Ime>/`
  (automatski se kopiraju u bin). Svaka koristi bar 2 formata (CSV + XML ili JSON).
- Zamjenski objekat: interfejs za modul koji još ne postoji staviti u `DigitalniKlubCitalaca/Services/`
  (npr. `IModeracijaService`), primati ga kao **opcioni** parametar konstruktora (`= null`), i u testu ga mockati Moq-om.
  Primjer: `IEmailService` u `ZahtjevZaPristupController`.
