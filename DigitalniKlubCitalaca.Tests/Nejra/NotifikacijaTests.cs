using System.Text.Json;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Services;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Nejra
{
    /// <summary>
    /// Autorica: Nejra Mašović
    /// Testirana klasa: NotifikacijaController
    /// </summary>
    [TestClass]
    public class NotifikacijaControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();
            var sada = DateTime.Now;

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Notifikacije.AddRange(
                    new Notifikacija { NotifikacijaId = 1, KorisnikId = "k1", Poruka = "Najstarija", Datum = sada.AddDays(-3) },
                    new Notifikacija { NotifikacijaId = 2, KorisnikId = "k1", Poruka = "Najnovija", Datum = sada.AddMinutes(-5) },
                    new Notifikacija { NotifikacijaId = 3, KorisnikId = "k1", Poruka = "Srednja", Datum = sada.AddDays(-1), Procitana = true },
                    new Notifikacija { NotifikacijaId = 4, KorisnikId = "k2", Poruka = "Tudja", Datum = sada });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private NotifikacijaController NapraviKontroler(Korisnik? prijavljeni)
        {
            return TestHelper.PripremiKontroler(
                new NotifikacijaController(_kontekst, TestHelper.UserManagerMock(prijavljeni).Object), prijavljeni?.Id);
        }

        [TestMethod]
        public async Task Index_NeprijavljenKorisnik_PreusmjeravaNaPocetnu()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.Index() as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual("Home", rezultat.ControllerName);
        }

        [TestMethod]
        public async Task Index_PrijavljenKorisnik_SamoNjegoveOdNajnovije()
        {
            var kontroler = NapraviKontroler(TestHelper.NoviKorisnik("k1"));

            var rezultat = await kontroler.Index() as ViewResult;

            var poruke = ((List<Notifikacija>)rezultat!.Model!).Select(n => n.Poruka).ToList();
            CollectionAssert.AreEqual(new List<string> { "Najnovija", "Srednja", "Najstarija" }, poruke);
        }

        [TestMethod]
        public async Task Index_OtvaranjeListe_SveNjegoveOznaceneKaoProcitaneUBazi()
        {
            var kontroler = NapraviKontroler(TestHelper.NoviKorisnik("k1"));

            await kontroler.Index();

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.IsTrue(provjera.Notifikacije.Where(n => n.KorisnikId == "k1").All(n => n.Procitana));
        }

        [TestMethod]
        public async Task Index_OtvaranjeListe_TudjeNotifikacijeOstajuNeprocitane()
        {
            var kontroler = NapraviKontroler(TestHelper.NoviKorisnik("k1"));

            await kontroler.Index();

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.IsFalse(provjera.Notifikacije.Single(n => n.KorisnikId == "k2").Procitana);
        }

        [TestMethod]
        public async Task Index_KorisnikBezNotifikacija_PraznaLista()
        {
            var kontroler = NapraviKontroler(TestHelper.NoviKorisnik("k3"));

            var rezultat = await kontroler.Index() as ViewResult;

            Assert.AreEqual(0, ((List<Notifikacija>)rezultat!.Model!).Count);
        }
    }

    /// <summary>
    /// Autorica: Nejra Mašović
    /// Testirana klasa: NotifikacijaService
    /// </summary>
    [TestClass]
    public class NotifikacijaServiceTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;
        private NotifikacijaService _servis = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();
            _kontekst = TestHelper.NoviKontekst(_opcije);
            _servis = new NotifikacijaService(_kontekst);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        public static IEnumerable<object?[]> NotifikacijeIzJson()
        {
            using var dokument = JsonDocument.Parse(File.ReadAllText(TestHelper.PutanjaPodataka("Nejra", "notifikacije.json")));
            return dokument.RootElement.EnumerateArray()
                .Select(e => new object?[]
                {
                    e.GetProperty("korisnikId").GetString(),
                    e.GetProperty("poruka").GetString(),
                    e.GetProperty("link").GetString()
                })
                .ToList();
        }

        [TestMethod]
        [DynamicData(nameof(NotifikacijeIzJson), DynamicDataSourceType.Method)]
        public async Task DodajNotifikacijuAsync_PodaciIzJson_SpremljenoTacnoKakoJePoslano(string korisnikId, string poruka, string? link)
        {
            await _servis.DodajNotifikacijuAsync(korisnikId, poruka, link);

            using var provjera = TestHelper.NoviKontekst(_opcije);
            var spremljena = provjera.Notifikacije.Single();
            Assert.AreEqual(korisnikId, spremljena.KorisnikId);
            Assert.AreEqual(poruka, spremljena.Poruka);
            Assert.AreEqual(link, spremljena.Link);
        }

        [TestMethod]
        public async Task DodajNotifikacijuAsync_NovaNotifikacija_NeprocitanaSaTrenutnimDatumom()
        {
            var prije = DateTime.Now;

            await _servis.DodajNotifikacijuAsync("k1", "Poruka");

            var spremljena = _kontekst.Notifikacije.Single();
            Assert.IsFalse(spremljena.Procitana);
            Assert.IsNull(spremljena.Link);
            Assert.IsTrue(spremljena.Datum >= prije && spremljena.Datum <= DateTime.Now);
        }

        [TestMethod]
        public async Task DodajNotifikacijuAsync_TriPoziva_TriZasebneNotifikacije()
        {
            await _servis.DodajNotifikacijuAsync("k1", "Prva");
            await _servis.DodajNotifikacijuAsync("k1", "Druga");
            await _servis.DodajNotifikacijuAsync("k2", "Treća");

            Assert.AreEqual(2, _kontekst.Notifikacije.Count(n => n.KorisnikId == "k1"));
            Assert.AreEqual(3, _kontekst.Notifikacije.Select(n => n.NotifikacijaId).Distinct().Count());
        }

        [TestMethod]
        public async Task DodajNotifikacijuAsync_KontekstZatvoren_BacaObjectDisposedException()
        {
            _kontekst.Dispose();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => _servis.DodajNotifikacijuAsync("k1", "Poruka"));
        }
    }
}
