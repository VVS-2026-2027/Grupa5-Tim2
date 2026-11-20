using System.Text.Json;
using System.Xml.Linq;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Services;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Nedzmija
{
    /// <summary>
    /// Autorica: Nedžmija Karić
    /// Testirana klasa: AdminController
    /// Zamjenski objekat: IDnevnikAktivnostiService (dnevnik admin akcija jos nije implementiran)
    /// </summary>
    [TestClass]
    public class AdminControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        private static Korisnik Osoba(string id, string ime, string prezime, string email, string grad, string regija,
            Uloga uloga, StatusNaloga status = StatusNaloga.aktivan)
        {
            return new Korisnik
            {
                Id = id, UserName = email, Email = email, Ime = ime, Prezime = prezime,
                Grad = grad, Regija = regija, ProfilnaSlika = string.Empty, Uloga = uloga, StatusNaloga = status
            };
        }

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Korisnici.AddRange(
                    Osoba("admin", "Admin", "Glavni", "admin@dkc.ba", "Sarajevo", "KS", Uloga.administrator),
                    Osoba("u1", "Amra", "Hodžić", "amra@dkc.ba", "Mostar", "HNK", Uloga.korisnik),
                    Osoba("u2", "Emir", "Begić", "emir@gmail.com", "Sarajevo", "KS", Uloga.moderator),
                    Osoba("u3", "Lana", "Amrić", "lana@dkc.ba", "Tuzla", "TK", Uloga.korisnik, StatusNaloga.blokiran),
                    Osoba("u4", "Tarik", "Mehić", "tarik@dkc.ba", "Zenica", "ZDK", Uloga.korisnik, StatusNaloga.neaktivan),
                    Osoba("admin2", "Selma", "Kovač", "selma@dkc.ba", "Bihać", "USK", Uloga.administrator));
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private AdminController NapraviKontroler(string? korisnikId, IDnevnikAktivnostiService? dnevnik = null)
        {
            return TestHelper.PripremiKontroler(new AdminController(_kontekst, dnevnik), korisnikId);
        }

        // ---------------- Data driven izvori ----------------

        public static IEnumerable<object[]> PretragaIzJson()
        {
            using var dokument = JsonDocument.Parse(File.ReadAllText(TestHelper.PutanjaPodataka("Nedzmija", "pretraga_korisnika.json")));
            return dokument.RootElement.EnumerateArray()
                .Select(e => new object[] { e.GetProperty("pretraga").GetString()!, e.GetProperty("ocekivaniBroj").GetInt32() })
                .ToList();
        }

        public static IEnumerable<object[]> StatusiIzXml()
        {
            return XDocument.Load(TestHelper.PutanjaPodataka("Nedzmija", "promjene_naloga.xml"))
                .Descendants("status")
                .Select(e => new object[] { (string)e.Attribute("korisnikId")!, Enum.Parse<StatusNaloga>((string)e.Attribute("ocekivano")!) });
        }

        public static IEnumerable<object[]> UlogeIzXml()
        {
            return XDocument.Load(TestHelper.PutanjaPodataka("Nedzmija", "promjene_naloga.xml"))
                .Descendants("uloga")
                .Select(e => new object[] { (string)e.Attribute("korisnikId")!, Enum.Parse<Uloga>((string)e.Attribute("ocekivano")!) });
        }

        // ---------------- Kontrola pristupa (sve akcije) ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("u1")]
        [DataRow("u2")]
        [DataRow("nepostojeci")]
        public async Task Index_KorisnikNijeAdministrator_PreusmjeravaNaAccessDenied(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.Index("") as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
            Assert.AreEqual("Home", rezultat.ControllerName);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("u2")]
        public async Task PromijeniStatus_KorisnikNijeAdministrator_StatusSeNeMijenja(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.PromijeniStatus("u1") as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
            Assert.AreEqual(StatusNaloga.aktivan, _kontekst.Korisnici.Single(k => k.Id == "u1").StatusNaloga);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("u2")]
        public async Task PromijeniUlogu_KorisnikNijeAdministrator_UlogaSeNeMijenja(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.PromijeniUlogu("u1") as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
            Assert.AreEqual(Uloga.korisnik, _kontekst.Korisnici.Single(k => k.Id == "u1").Uloga);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("u1")]
        public async Task Postavke_KorisnikNijeAdministrator_PreusmjeravaNaAccessDenied(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.Postavke() as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("u2")]
        public async Task DnevnikAktivnosti_KorisnikNijeAdministrator_PreusmjeravaNaAccessDenied(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.DnevnikAktivnosti() as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
        }

        // ---------------- Index (pretraga) ----------------

        [TestMethod]
        [DynamicData(nameof(PretragaIzJson), DynamicDataSourceType.Method)]
        public async Task Index_PretragaIzJson_VracaOcekivaniBrojKorisnika(string pretraga, int ocekivaniBroj)
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Index(pretraga) as ViewResult;

            Assert.AreEqual(ocekivaniBroj, ((List<Korisnik>)rezultat!.Model!).Count);
        }

        [TestMethod]
        public async Task Index_PretragaPoGradu_VracaTacnoTeKorisnikeIPamtiPojam()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Index("Sarajevo") as ViewResult;

            var ids = ((List<Korisnik>)rezultat!.Model!).Select(k => k.Id).ToList();
            CollectionAssert.AreEquivalent(new List<string> { "admin", "u2" }, ids);
            Assert.AreEqual("Sarajevo", kontroler.ViewBag.Pretraga);
        }

        // ---------------- PromijeniStatus ----------------

        [TestMethod]
        [DynamicData(nameof(StatusiIzXml), DynamicDataSourceType.Method)]
        public async Task PromijeniStatus_PocetniStatusIzXml_PrelaziUOcekivaniStatus(string korisnikId, StatusNaloga ocekivano)
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.PromijeniStatus(korisnikId) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(ocekivano, _kontekst.Korisnici.Single(k => k.Id == korisnikId).StatusNaloga);
        }

        [TestMethod]
        public async Task PromijeniStatus_DvaPuta_VracaNaPocetniStatus()
        {
            var kontroler = NapraviKontroler("admin");

            await kontroler.PromijeniStatus("u1");
            await kontroler.PromijeniStatus("u1");

            Assert.AreEqual(StatusNaloga.aktivan, _kontekst.Korisnici.Single(k => k.Id == "u1").StatusNaloga);
        }

        [TestMethod]
        public async Task PromijeniStatus_NepostojeciKorisnik_VracaNotFound()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.PromijeniStatus("nepostojeci");

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        // ---------------- PromijeniUlogu ----------------

        [TestMethod]
        [DynamicData(nameof(UlogeIzXml), DynamicDataSourceType.Method)]
        public async Task PromijeniUlogu_PocetnaUlogaIzXml_PrelaziUOcekivanuUlogu(string korisnikId, Uloga ocekivano)
        {
            var kontroler = NapraviKontroler("admin");

            await kontroler.PromijeniUlogu(korisnikId);

            Assert.AreEqual(ocekivano, _kontekst.Korisnici.Single(k => k.Id == korisnikId).Uloga);
        }

        [TestMethod]
        public async Task PromijeniUlogu_NepostojeciKorisnik_VracaNotFound()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.PromijeniUlogu("nepostojeci");

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task PromijeniUlogu_AdministratorSamomSebi_GubiAdministratorskaPrava()
        {
            var kontroler = NapraviKontroler("admin");

            await kontroler.PromijeniUlogu("admin");
            var drugiPokusaj = await kontroler.Index("") as RedirectToActionResult;

            Assert.AreEqual(Uloga.moderator, _kontekst.Korisnici.Single(k => k.Id == "admin").Uloga);
            Assert.AreEqual("AccessDenied", drugiPokusaj!.ActionName);
        }

        // ---------------- Postavke / DnevnikAktivnosti ----------------

        [TestMethod]
        public async Task Postavke_Administrator_VracaView()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Postavke();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public async Task DnevnikAktivnosti_Administrator_VracaView()
        {
            var kontroler = NapraviKontroler("admin2");

            var rezultat = await kontroler.DnevnikAktivnosti();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        // ---------------- Zamjenski objekat: IDnevnikAktivnostiService (mock) ----------------

        [TestMethod]
        public async Task PromijeniStatus_UspjesnaPromjena_BiljeziSeUDnevnikJednom()
        {
            var dnevnik = new Mock<IDnevnikAktivnostiService>();
            var kontroler = NapraviKontroler("admin", dnevnik.Object);

            await kontroler.PromijeniStatus("u1");

            dnevnik.Verify(d => d.ZabiljeziAsync("admin", "PromijeniStatus", "u1"), Times.Once);
        }

        [TestMethod]
        public async Task PromijeniUlogu_UspjesnaPromjena_BiljeziSeAdminKojiJePromijenio()
        {
            var dnevnik = new Mock<IDnevnikAktivnostiService>();
            var kontroler = NapraviKontroler("admin2", dnevnik.Object);

            await kontroler.PromijeniUlogu("u2");

            dnevnik.Verify(d => d.ZabiljeziAsync("admin2", "PromijeniUlogu", "u2"), Times.Once);
            dnevnik.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task PromijeniStatus_BezPravaIliNepostojeciKorisnik_NistaSeNeBiljezi()
        {
            var dnevnik = new Mock<IDnevnikAktivnostiService>();
            var kontrolerModeratora = NapraviKontroler("u2", dnevnik.Object);
            var kontrolerAdmina = NapraviKontroler("admin", dnevnik.Object);

            await kontrolerModeratora.PromijeniStatus("u1");
            await kontrolerAdmina.PromijeniStatus("nepostojeci");

            dnevnik.Verify(d => d.ZabiljeziAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task PromijeniUlogu_DnevnikNedostupan_IzuzetakAliJeUlogaVecPromijenjena()
        {
            var dnevnik = new Mock<IDnevnikAktivnostiService>();
            dnevnik.Setup(d => d.ZabiljeziAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                   .ThrowsAsync(new TimeoutException("Dnevnik ne odgovara"));
            var kontroler = NapraviKontroler("admin", dnevnik.Object);

            await Assert.ThrowsExceptionAsync<TimeoutException>(() => kontroler.PromijeniUlogu("u1"));

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual(Uloga.moderator, provjera.Korisnici.Single(k => k.Id == "u1").Uloga);
        }
    }
}
