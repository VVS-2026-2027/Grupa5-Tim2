using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Nedzmija
{
    /// <summary>
    /// Autorica: Nedžmija Karić
    /// Testirana klasa: KorisnikController (namespace DigitalniKlubCitalaca)
    /// </summary>
    [TestClass]
    public class KorisnikControllerTests
    {
        // Pravi Identity kljuc je GUID string - testovi koriste takav id
        private const string Guid1 = "3f2b8c1e-0a4d-4e8b-9b1a-7c5d2e6f9a01";
        private const string Guid2 = "8a7d6c5b-4e3f-4a2b-8c1d-0e9f8a7b6c02";

        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Korisnici.AddRange(TestHelper.NoviKorisnik(Guid1), TestHelper.NoviKorisnik(Guid2, Uloga.moderator));
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private KorisnikController NapraviKontroler()
        {
            return TestHelper.PripremiKontroler(new KorisnikController(_kontekst), Guid1);
        }

        // ---------------- Index / Details ----------------

        [TestMethod]
        public async Task Index_DvaKorisnika_VracaListuSaObaKorisnika()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Index() as ViewResult;

            Assert.AreEqual(2, ((List<Korisnik>)rezultat!.Model!).Count);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("nepostojeci-id")]
        public async Task Details_IdNullIliNepostojeci_VracaNotFound(string? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciKorisnik_VracaKorisnika()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(Guid2) as ViewResult;

            Assert.AreEqual(Uloga.moderator, ((Korisnik)rezultat!.Model!).Uloga);
        }

        // ---------------- Create ----------------

        [TestMethod]
        public void Create_Get_VracaPrazanView()
        {
            var kontroler = NapraviKontroler();

            Assert.IsInstanceOfType(kontroler.Create(), typeof(ViewResult));
        }

        [TestMethod]
        public async Task Create_Post_IspravanKorisnik_SpremaIPreusmjerava()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Create(TestHelper.NoviKorisnik("novi")) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(3, _kontekst.Korisnici.Count());
        }

        [TestMethod]
        public async Task Create_Post_NevalidanModel_VracaViewBezSpremanja()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("Email", "Neispravan e-mail");
            var korisnik = TestHelper.NoviKorisnik("novi");

            var rezultat = await kontroler.Create(korisnik) as ViewResult;

            Assert.AreSame(korisnik, rezultat!.Model);
            Assert.AreEqual(2, _kontekst.Korisnici.Count());
        }

        // ---------------- Edit (GET) ----------------
        // Regresioni testovi: prije ispravke akcija je primala int? pa Edit za GUID nikad nije radio.

        [TestMethod]
        [DataRow(null)]
        [DataRow("nepostojeci-id")]
        public async Task Edit_Get_IdNullIliNepostojeci_VracaNotFound(string? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_GuidKorisnika_VracaViewSaKorisnikom()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(Guid1) as ViewResult;

            Assert.AreEqual(Guid1, ((Korisnik)rezultat!.Model!).Id);
        }

        // ---------------- Edit (POST) ----------------

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(Guid1, TestHelper.NoviKorisnik(Guid2));

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_IspravnaIzmjena_MijenjaStatusNaloga()
        {
            var kontroler = NapraviKontroler();
            var izmjena = TestHelper.NoviKorisnik(Guid2, Uloga.moderator);
            izmjena.StatusNaloga = StatusNaloga.blokiran;

            var rezultat = await kontroler.Edit(Guid2, izmjena) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(StatusNaloga.blokiran, provjera.Korisnici.Single(k => k.Id == Guid2).StatusNaloga);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("Ime", "Obavezno");
            var izmjena = TestHelper.NoviKorisnik(Guid1);

            var rezultat = await kontroler.Edit(Guid1, izmjena) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
        }

        [TestMethod]
        public async Task Edit_Post_KorisnikObrisanUMedjuvremenu_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit("obrisan", TestHelper.NoviKorisnik("obrisan"));

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnaIzmjena_BacaDbUpdateConcurrencyException()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(new KorisnikController(stubKontekst), Guid1);

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(Guid1, TestHelper.NoviKorisnik(Guid1)));
        }

        // ---------------- Delete ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("nepostojeci-id")]
        public async Task Delete_IdNullIliNepostojeci_VracaNotFound(string? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_PostojeciKorisnik_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(Guid2) as ViewResult;

            Assert.AreEqual(Guid2, ((Korisnik)rezultat!.Model!).Id);
        }

        // Regresioni test: prije ispravke DeleteConfirmed je primao int pa je Find bacao ArgumentException.
        [TestMethod]
        public async Task DeleteConfirmed_GuidKorisnika_BriseKorisnika()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.DeleteConfirmed(Guid2) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            CollectionAssert.AreEqual(new List<string> { Guid1 }, _kontekst.Korisnici.Select(k => k.Id).ToList());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciKorisnik_NistaNeBrise()
        {
            var kontroler = NapraviKontroler();

            await kontroler.DeleteConfirmed("nepostojeci-id");

            Assert.AreEqual(2, _kontekst.Korisnici.Count());
        }

        // Dokaz da je greska postojala: Find sa int vrijednoscu nad string kljucem baca ArgumentException
        [TestMethod]
        public async Task Find_IntVrijednostNadGuidKljucem_BacaArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await _kontekst.Korisnici.FindAsync(5));
        }
    }
}
