using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Nedzmija
{
    /// <summary>
    /// Autorica: Nedžmija Karić
    /// Testirana klasa: ProfilController
    /// </summary>
    [TestClass]
    public class ProfilControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Korisnici.AddRange(TestHelper.NoviKorisnik("k1"), TestHelper.NoviKorisnik("k2"), TestHelper.NoviKorisnik("k3"));
                seed.Profili.AddRange(
                    new Profil { ProfilId = 1, KorisnikId = "k1", OpisProfila = "Stari opis", Lokacija = "Tuzla" },
                    new Profil { ProfilId = 2, KorisnikId = "k2", OpisProfila = "Profil od k2", Lokacija = "Zenica" });
                seed.ClanstvaGrupe.AddRange(
                    new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = "k1", GrupaId = 1 },
                    new ClanstvoGrupe { ClanstvoId = 2, KorisnikId = "k1", GrupaId = 2 },
                    new ClanstvoGrupe { ClanstvoId = 3, KorisnikId = "k2", GrupaId = 1 });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private ProfilController NapraviKontroler(string? korisnikId)
        {
            return TestHelper.PripremiKontroler(new ProfilController(_kontekst), korisnikId);
        }

        // ---------------- Data driven izvor (CSV) ----------------

        public static IEnumerable<object[]> IzmjeneIzCsv()
        {
            return File.ReadAllLines(TestHelper.PutanjaPodataka("Nedzmija", "izmjena_profila.csv"))
                .Skip(1)
                .Select(red => red.Split(';'))
                .Select(p => new object[] { p[0], p[1], p[2], p[3], p[4] });
        }

        // ---------------- Index ----------------

        [TestMethod]
        public async Task Index_KorisnikBezProfila_PreusmjeravaNaKreiranjeProfila()
        {
            var kontroler = NapraviKontroler("k3");

            var rezultat = await kontroler.Index() as RedirectToActionResult;

            Assert.AreEqual("Create", rezultat!.ActionName);
        }

        [TestMethod]
        public async Task Index_KorisnikSaProfilom_VracaSvojProfilIBrojGrupa()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Index() as ViewResult;

            var profili = (List<Profil>)rezultat!.Model!;
            Assert.AreEqual(1, profili.Count);
            Assert.AreEqual("k1", profili[0].KorisnikId);
            Assert.AreEqual(2, (int)kontroler.ViewBag.BrojGrupa);
        }

        // ---------------- Details ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Details_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciProfil_VracaProfilSaKorisnikom()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Details(2) as ViewResult;

            var profil = (Profil)rezultat!.Model!;
            Assert.AreEqual("Zenica", profil.Lokacija);
            Assert.AreEqual("Imek2", profil.Korisnik.Ime);
        }

        // ---------------- Create ----------------

        [TestMethod]
        public void Create_Get_VracaPrazanView()
        {
            var kontroler = NapraviKontroler("k3");

            var rezultat = kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public async Task Create_Post_NeprijavljenKorisnik_PreusmjeravaNaLogin()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.Create(new Profil { OpisProfila = "Opis" });

            Assert.AreEqual("/Account/Login", ((RedirectToPageResult)rezultat).PageName);
        }

        [TestMethod]
        public async Task Create_Post_IspravanProfil_VezujeSeZaPrijavljenogKorisnikaIObavjestava()
        {
            var kontroler = NapraviKontroler("k3");

            var rezultat = await kontroler.Create(new Profil { ProfilId = 3, KorisnikId = "k1", OpisProfila = "Novi", Lokacija = "Bihać" }) as RedirectToActionResult;

            var notifikacija = _kontekst.Notifikacije.Single();
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual("k3", _kontekst.Profili.Single(p => p.ProfilId == 3).KorisnikId);
            Assert.AreEqual("k3", notifikacija.KorisnikId);
            Assert.AreEqual("Vaš profil je uspješno kreiran.", notifikacija.Poruka);
        }

        [TestMethod]
        public async Task Create_Post_NevalidanModel_VracaViewBezSpremanja()
        {
            var kontroler = NapraviKontroler("k3");
            kontroler.ModelState.AddModelError("OpisProfila", "Predug opis");
            var profil = new Profil { ProfilId = 3, OpisProfila = "x" };

            var rezultat = await kontroler.Create(profil) as ViewResult;

            Assert.AreSame(profil, rezultat!.Model);
            Assert.AreEqual(2, _kontekst.Profili.Count());
            Assert.AreEqual(0, _kontekst.Notifikacije.Count());
        }

        // ---------------- Edit (GET) ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        [DataRow(2)]
        public async Task Edit_Get_IdNullNepostojeciIliTudjiProfil_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Edit(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_SvojProfil_VracaViewSaProfilom()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Edit(1) as ViewResult;

            Assert.AreEqual("Stari opis", ((Profil)rezultat!.Model!).OpisProfila);
        }

        // ---------------- Edit (POST) ----------------

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Edit(1, new Profil { ProfilId = 2 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_TudjiProfil_VracaNotFoundINistaNeMijenja()
        {
            var kontroler = NapraviKontroler("k1");
            var pokusaj = new Profil { ProfilId = 2, OpisProfila = "Hakovano", Korisnik = new Korisnik { Ime = "X" } };

            var rezultat = await kontroler.Edit(2, pokusaj);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
            Assert.AreEqual("Profil od k2", _kontekst.Profili.Single(p => p.ProfilId == 2).OpisProfila);
        }

        [TestMethod]
        [DynamicData(nameof(IzmjeneIzCsv), DynamicDataSourceType.Method)]
        public async Task Edit_Post_PodaciIzCsv_ProfilIKorisnikSinhronizovani(string opis, string lokacija, string ime, string prezime, string regija)
        {
            var kontroler = NapraviKontroler("k1");
            var izmjena = new Profil
            {
                ProfilId = 1, OpisProfila = opis, Lokacija = lokacija,
                Korisnik = new Korisnik { Ime = ime, Prezime = prezime, Regija = regija }
            };

            var rezultat = await kontroler.Edit(1, izmjena) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            var profil = provjera.Profili.Include(p => p.Korisnik).Single(p => p.ProfilId == 1);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(opis, profil.OpisProfila);
            Assert.AreEqual(lokacija, profil.Lokacija);
            Assert.AreEqual(ime, profil.Korisnik.Ime);
            Assert.AreEqual(prezime, profil.Korisnik.Prezime);
            Assert.AreEqual(regija, profil.Korisnik.Regija);
            Assert.AreEqual(lokacija, profil.Korisnik.Grad);
            Assert.AreEqual(opis, profil.Korisnik.Biografija);
        }

        [TestMethod]
        public async Task Edit_Post_IspravnaIzmjena_NotifikacijaIPorukaOUspjehu()
        {
            var kontroler = NapraviKontroler("k1");
            var izmjena = new Profil { ProfilId = 1, OpisProfila = "Novi", Lokacija = "Sarajevo", Korisnik = new Korisnik { Ime = "A", Prezime = "B" } };

            await kontroler.Edit(1, izmjena);

            Assert.AreEqual("Vaš profil je uspješno ažuriran.", _kontekst.Notifikacije.Single().Poruka);
            Assert.AreEqual("Profil je uspješno ažuriran!", kontroler.TempData["SuccessMessage"]);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaProfilIzBazeBezIzmjena()
        {
            var kontroler = NapraviKontroler("k1");
            kontroler.ModelState.AddModelError("Lokacija", "Neispravna lokacija");

            var rezultat = await kontroler.Edit(1, new Profil { ProfilId = 1, OpisProfila = "Neće se spremiti" }) as ViewResult;

            Assert.AreEqual("Stari opis", ((Profil)rezultat!.Model!).OpisProfila);
            Assert.AreEqual(0, _kontekst.Notifikacije.Count());
        }

        // ---------------- Delete ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Delete_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_PostojeciProfil_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Delete(1) as ViewResult;

            Assert.AreEqual(1, ((Profil)rezultat!.Model!).ProfilId);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciProfil_BriseProfilAliNeKorisnika()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.IsFalse(_kontekst.Profili.Any(p => p.ProfilId == 1));
            Assert.IsTrue(_kontekst.Korisnici.Any(k => k.Id == "k1"));
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciProfil_NistaNeBrise()
        {
            var kontroler = NapraviKontroler("k1");

            await kontroler.DeleteConfirmed(999);

            Assert.AreEqual(2, _kontekst.Profili.Count());
        }

        // ---------------- PromijeniProfilnuSliku ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("nepostojeci")]
        public async Task PromijeniProfilnuSliku_NepoznatKorisnik_VracaNotFound(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.PromijeniProfilnuSliku("/img/avatar3.png");

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task PromijeniProfilnuSliku_PrijavljenKorisnik_SlikaPromijenjenaSamoNjemu()
        {
            var kontroler = NapraviKontroler("k2");

            var rezultat = await kontroler.PromijeniProfilnuSliku("/img/avatar3.png") as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual("/img/avatar3.png", _kontekst.Korisnici.Single(k => k.Id == "k2").ProfilnaSlika);
            Assert.AreEqual(string.Empty, _kontekst.Korisnici.Single(k => k.Id == "k1").ProfilnaSlika);
            Assert.AreEqual("Profilna slika je uspješno promijenjena!", kontroler.TempData["SuccessMessage"]);
        }
    }
}
