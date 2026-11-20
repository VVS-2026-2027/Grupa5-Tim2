using System.Text.Json;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Services;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Lejla
{
    /// <summary>
    /// Autorica: Lejla Bajrić
    /// Testirana klasa: ZahtjevZaPristupController
    /// Zamjenski objekat: IEmailService (modul za e-mail obavijesti jos nije implementiran)
    /// </summary>
    [TestClass]
    public class ZahtjevZaPristupControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Korisnici.AddRange(TestHelper.NoviKorisnik("k1"), TestHelper.NoviKorisnik("k2"));
                seed.CitalackeGrupe.AddRange(
                    new CitalackaGrupa { GrupaId = 1, Naziv = "Privatni krimi klub", TipGrupe = TipGrupe.privatna },
                    new CitalackaGrupa { GrupaId = 2, Naziv = "Pozivnica poezija", TipGrupe = TipGrupe.pozivnica });
                seed.ZahtjeviZaPristup.AddRange(
                    new ZahtjevZaPristup { ZahtjevId = 1, KorisnikId = "k1", GrupaId = 1, StatusZahtjeva = StatusZahtjeva.na_cekanju, DatumZahtjeva = new DateTime(2026, 11, 1) },
                    new ZahtjevZaPristup { ZahtjevId = 2, KorisnikId = "k2", GrupaId = 2, StatusZahtjeva = StatusZahtjeva.na_cekanju, DatumZahtjeva = new DateTime(2026, 11, 2) });
                // k2 je vec clan grupe 1 (npr. dodan rucno), ali nije grupe 2
                seed.ClanstvaGrupe.Add(new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = "k2", GrupaId = 1, StatusClanstva = StatusClanstva.clan });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private ZahtjevZaPristupController NapraviKontroler(IEmailService? emailService = null)
        {
            return TestHelper.PripremiKontroler(new ZahtjevZaPristupController(_kontekst, emailService), "admin");
        }

        // ---------------- Data driven izvor (JSON) ----------------

        private record SlucajOdobravanja(string NazivGrupe, string OcekivanaPoruka);

        public static IEnumerable<object[]> OdobravanjaIzJson()
        {
            var json = File.ReadAllText(TestHelper.PutanjaPodataka("Lejla", "odobravanje_zahtjeva.json"));
            var slucajevi = JsonSerializer.Deserialize<List<SlucajOdobravanja>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return slucajevi.Select(s => new object[] { s.NazivGrupe, s.OcekivanaPoruka });
        }

        // ---------------- Index / Details ----------------

        [TestMethod]
        public async Task Index_PostojeDvaZahtjeva_VracaObaSaUcitanomGrupomIKorisnikom()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Index() as ViewResult;

            var zahtjevi = (List<ZahtjevZaPristup>)rezultat!.Model!;
            Assert.AreEqual(2, zahtjevi.Count);
            Assert.IsTrue(zahtjevi.All(z => z.CitalackaGrupa != null && z.Korisnik != null));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Details_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciZahtjev_VracaZahtjev()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(2) as ViewResult;

            Assert.AreEqual("k2", ((ZahtjevZaPristup)rezultat!.Model!).KorisnikId);
        }

        // ---------------- Create ----------------

        [TestMethod]
        public void Create_Get_PunViewDataSaGrupamaIKorisnicima()
        {
            var kontroler = NapraviKontroler();

            kontroler.Create();

            Assert.AreEqual(2, ((SelectList)kontroler.ViewData["GrupaId"]!).Count());
            Assert.AreEqual(2, ((SelectList)kontroler.ViewData["KorisnikId"]!).Count());
        }

        [TestMethod]
        public async Task Create_ValidanZahtjev_SpremaIPreusmjerava()
        {
            var kontroler = NapraviKontroler();
            var novi = new ZahtjevZaPristup { ZahtjevId = 3, KorisnikId = "k1", GrupaId = 2, StatusZahtjeva = StatusZahtjeva.na_cekanju };

            var rezultat = await kontroler.Create(novi) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(3, _kontekst.ZahtjeviZaPristup.Count());
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaViewINePunioBazu()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("GrupaId", "Obavezno");
            var novi = new ZahtjevZaPristup { ZahtjevId = 3, KorisnikId = "k1", GrupaId = 2 };

            var rezultat = await kontroler.Create(novi) as ViewResult;

            Assert.AreSame(novi, rezultat!.Model);
            Assert.IsNotNull(kontroler.ViewData["KorisnikId"]);
            Assert.AreEqual(2, _kontekst.ZahtjeviZaPristup.Count());
        }

        // ---------------- Edit ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Edit_Get_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciZahtjev_VracaViewSaPadajucimListama()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1) as ViewResult;

            Assert.AreEqual(1, ((ZahtjevZaPristup)rezultat!.Model!).ZahtjevId);
            Assert.IsInstanceOfType(kontroler.ViewData["GrupaId"], typeof(SelectList));
        }

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1, new ZahtjevZaPristup { ZahtjevId = 2 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_ValidnaIzmjena_MijenjaStatus()
        {
            var kontroler = NapraviKontroler();
            var izmjena = new ZahtjevZaPristup { ZahtjevId = 1, KorisnikId = "k1", GrupaId = 1, StatusZahtjeva = StatusZahtjeva.odbijen };

            var rezultat = await kontroler.Edit(1, izmjena) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(StatusZahtjeva.odbijen, provjera.ZahtjeviZaPristup.Single(z => z.ZahtjevId == 1).StatusZahtjeva);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("StatusZahtjeva", "Neispravan status");
            var izmjena = new ZahtjevZaPristup { ZahtjevId = 1, KorisnikId = "k1", GrupaId = 1 };

            var rezultat = await kontroler.Edit(1, izmjena) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
        }

        [TestMethod]
        public async Task Edit_Post_ZahtjevObrisanUMedjuvremenu_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(500, new ZahtjevZaPristup { ZahtjevId = 500, KorisnikId = "k1", GrupaId = 1 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnaIzmjenaPostojecegZahtjeva_BacaIzuzetak()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(new ZahtjevZaPristupController(stubKontekst), "admin");

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(1, new ZahtjevZaPristup { ZahtjevId = 1, KorisnikId = "k1", GrupaId = 1 }));
        }

        // ---------------- Odobri ----------------

        [TestMethod]
        public async Task Odobri_NepostojeciZahtjev_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Odobri(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Odobri_KorisnikNijeClan_MijenjaStatusIDodajeClanstvo()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Odobri(1) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(StatusZahtjeva.odobren, _kontekst.ZahtjeviZaPristup.Single(z => z.ZahtjevId == 1).StatusZahtjeva);
            Assert.AreEqual(StatusClanstva.clan, _kontekst.ClanstvaGrupe.Single(c => c.KorisnikId == "k1").StatusClanstva);
        }

        [TestMethod]
        public async Task Odobri_KorisnikVecClanGrupe_NeDupliraClanstvo()
        {
            _kontekst.ZahtjeviZaPristup.Add(new ZahtjevZaPristup { ZahtjevId = 3, KorisnikId = "k2", GrupaId = 1, StatusZahtjeva = StatusZahtjeva.na_cekanju });
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler();

            await kontroler.Odobri(3);

            Assert.AreEqual(1, _kontekst.ClanstvaGrupe.Count(c => c.KorisnikId == "k2" && c.GrupaId == 1));
        }

        [TestMethod]
        [DynamicData(nameof(OdobravanjaIzJson), DynamicDataSourceType.Method)]
        public async Task Odobri_NazivGrupeIzJson_NotifikacijaSadrziIspravnuPoruku(string nazivGrupe, string ocekivanaPoruka)
        {
            _kontekst.CitalackeGrupe.Add(new CitalackaGrupa { GrupaId = 10, Naziv = nazivGrupe, TipGrupe = TipGrupe.privatna });
            _kontekst.ZahtjeviZaPristup.Add(new ZahtjevZaPristup { ZahtjevId = 10, KorisnikId = "k1", GrupaId = 10, StatusZahtjeva = StatusZahtjeva.na_cekanju });
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler();

            await kontroler.Odobri(10);

            Assert.AreEqual(ocekivanaPoruka, _kontekst.Notifikacije.Single().Poruka);
        }

        // ---------------- Odbij ----------------

        [TestMethod]
        public async Task Odbij_NepostojeciZahtjev_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Odbij(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Odbij_PostojeciZahtjev_StatusOdbijenBezNovogClanstva()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Odbij(2) as RedirectToActionResult;

            var notifikacija = _kontekst.Notifikacije.Single();
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(StatusZahtjeva.odbijen, _kontekst.ZahtjeviZaPristup.Single(z => z.ZahtjevId == 2).StatusZahtjeva);
            Assert.IsFalse(_kontekst.ClanstvaGrupe.Any(c => c.GrupaId == 2));
            Assert.AreEqual("Vaš zahtjev za pristup grupi \"Pozivnica poezija\" je odbijen.", notifikacija.Poruka);
            Assert.AreEqual("/test-link", notifikacija.Link);
        }

        // ---------------- Zamjenski objekat: IEmailService (mock) ----------------

        [TestMethod]
        public async Task Odobri_EmailServisPostoji_SaljeTacnoJedanMailPodnosiocu()
        {
            var emailMock = new Mock<IEmailService>();
            var kontroler = NapraviKontroler(emailMock.Object);

            await kontroler.Odobri(1);

            emailMock.Verify(e => e.PosaljiObavijestAsync("k1", "Zahtjev odobren",
                "Vaš zahtjev za pristup grupi \"Privatni krimi klub\" je odobren."), Times.Once);
        }

        [TestMethod]
        public async Task Odbij_EmailServisPostoji_SaljeMailOOdbijanju()
        {
            var emailMock = new Mock<IEmailService>();
            var kontroler = NapraviKontroler(emailMock.Object);

            await kontroler.Odbij(2);

            emailMock.Verify(e => e.PosaljiObavijestAsync("k2", "Zahtjev odbijen", It.Is<string>(p => p.Contains("odbijen"))), Times.Once);
        }

        [TestMethod]
        public async Task Odobri_NepostojeciZahtjev_EmailSeNeSalje()
        {
            var emailMock = new Mock<IEmailService>();
            var kontroler = NapraviKontroler(emailMock.Object);

            await kontroler.Odobri(999);

            emailMock.Verify(e => e.PosaljiObavijestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task Odobri_EmailServisNedostupan_IzuzetakSePropagiraAliJeStatusVecSpremljen()
        {
            var emailMock = new Mock<IEmailService>();
            emailMock.Setup(e => e.PosaljiObavijestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                     .ThrowsAsync(new InvalidOperationException("SMTP server nedostupan"));
            var kontroler = NapraviKontroler(emailMock.Object);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => kontroler.Odobri(1));

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual(StatusZahtjeva.odobren, provjera.ZahtjeviZaPristup.Single(z => z.ZahtjevId == 1).StatusZahtjeva);
        }

        // ---------------- Delete ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Delete_Get_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_Get_PostojeciZahtjev_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(1) as ViewResult;

            Assert.AreEqual(1, ((ZahtjevZaPristup)rezultat!.Model!).ZahtjevId);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciZahtjev_BriseZahtjev()
        {
            var kontroler = NapraviKontroler();

            await kontroler.DeleteConfirmed(1);

            Assert.IsFalse(_kontekst.ZahtjeviZaPristup.Any(z => z.ZahtjevId == 1));
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciZahtjev_PreusmjeravaBezBrisanja()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.DeleteConfirmed(999) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(2, _kontekst.ZahtjeviZaPristup.Count());
        }
    }
}
