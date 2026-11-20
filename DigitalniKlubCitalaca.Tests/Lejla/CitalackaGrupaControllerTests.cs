using System.Xml.Linq;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Lejla
{
    /// <summary>
    /// Autorica: Lejla Bajrić
    /// Testirana klasa: CitalackaGrupaController
    /// </summary>
    [TestClass]
    public class CitalackaGrupaControllerTests
    {
        private const string KorisnikId = "k1";

        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;
        private Korisnik _korisnik = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();
            _korisnik = TestHelper.NoviKorisnik(KorisnikId);

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Korisnici.Add(_korisnik);
                seed.CitalackeGrupe.AddRange(
                    new CitalackaGrupa { GrupaId = 1, Naziv = "Krimi SA", Regija = "Sarajevo", Zanr = "Krimi", TipGrupe = TipGrupe.javna, DatumKreiranja = new DateTime(2026, 1, 1) },
                    new CitalackaGrupa { GrupaId = 2, Naziv = "Fantastika SA", Regija = "Sarajevo", Zanr = "Fantastika", TipGrupe = TipGrupe.javna, DatumKreiranja = new DateTime(2026, 3, 1) },
                    new CitalackaGrupa { GrupaId = 3, Naziv = "Roman MO", Regija = "Mostar", Zanr = "Roman", TipGrupe = TipGrupe.privatna, DatumKreiranja = new DateTime(2026, 2, 1) },
                    new CitalackaGrupa { GrupaId = 4, Naziv = "Krimi BL", Regija = "Banja Luka", Zanr = "Krimi", TipGrupe = TipGrupe.javna, DatumKreiranja = new DateTime(2026, 4, 1) },
                    new CitalackaGrupa { GrupaId = 5, Naziv = "Roman SA", Regija = "Sarajevo", Zanr = "Roman", TipGrupe = TipGrupe.javna, DatumKreiranja = new DateTime(2026, 5, 1) },
                    new CitalackaGrupa { GrupaId = 6, Naziv = "Drama TZ", Regija = "Tuzla", Zanr = "Drama", TipGrupe = TipGrupe.privatna, DatumKreiranja = new DateTime(2026, 6, 1) });

                // korisnik je clan grupe 5 i ima zahtjev na cekanju za grupu 6
                seed.ClanstvaGrupe.Add(new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = KorisnikId, GrupaId = 5, StatusClanstva = StatusClanstva.clan });
                seed.ZahtjeviZaPristup.Add(new ZahtjevZaPristup { ZahtjevId = 1, KorisnikId = KorisnikId, GrupaId = 6, StatusZahtjeva = StatusZahtjeva.na_cekanju });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private CitalackaGrupaController NapraviKontroler(Korisnik? prijavljeni)
        {
            var userManager = TestHelper.UserManagerMock(prijavljeni);
            return TestHelper.PripremiKontroler(
                new CitalackaGrupaController(_kontekst, userManager.Object), prijavljeni?.Id);
        }

        // ---------------- Data driven izvori ----------------

        public static IEnumerable<object[]> FilteriIzCsv()
        {
            return File.ReadAllLines(TestHelper.PutanjaPodataka("Lejla", "dostupne_grupe_filteri.csv"))
                .Skip(1)
                .Select(red => red.Split(';'))
                .Select(p => new object[] { p[0], p[1], int.Parse(p[2]) });
        }

        public static IEnumerable<object[]> PridruzivanjaIzXml()
        {
            return XDocument.Load(TestHelper.PutanjaPodataka("Lejla", "pridruzivanje_grupi.xml"))
                .Descendants("slucaj")
                .Select(s => new object[]
                {
                    Enum.Parse<TipGrupe>((string)s.Element("tipGrupe")!),
                    (int)s.Element("ocekivanoClanstava")!,
                    (int)s.Element("ocekivanoZahtjeva")!,
                    (int)s.Element("ocekivanoNotifikacija")!
                });
        }

        // ---------------- Index ----------------

        [TestMethod]
        public async Task Index_NeprijavljenKorisnik_PreusmjeravaNaLogin()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.Index();

            var redirect = rezultat as RedirectToPageResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("/Account/Login", redirect.PageName);
            Assert.AreEqual("Identity", redirect.RouteValues!["area"]);
        }

        [TestMethod]
        public async Task Index_KorisnikClanJedneGrupe_VracaSamoTuGrupu()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Index() as ViewResult;

            var grupe = rezultat!.Model as List<CitalackaGrupa>;
            Assert.AreEqual(1, grupe!.Count);
            Assert.AreEqual(5, grupe[0].GrupaId);
        }

        // ---------------- DostupneGrupe ----------------

        [TestMethod]
        public async Task DostupneGrupe_NeprijavljenKorisnik_PreusmjeravaNaLogin()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.DostupneGrupe(null, null);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
        }

        [TestMethod]
        [DynamicData(nameof(FilteriIzCsv), DynamicDataSourceType.Method)]
        public async Task DostupneGrupe_FilteriIzCsv_VracaOcekivaniBrojGrupa(string regija, string zanr, int ocekivaniBroj)
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.DostupneGrupe(regija, zanr) as ViewResult;

            var grupe = rezultat!.Model as List<CitalackaGrupa>;
            Assert.AreEqual(ocekivaniBroj, grupe!.Count);
        }

        [TestMethod]
        public async Task DostupneGrupe_ClanstvoIZahtjevNaCekanju_IskljuceneGrupeKojeKorisnikVecIma()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.DostupneGrupe(null, null) as ViewResult;

            var ids = (rezultat!.Model as List<CitalackaGrupa>)!.Select(g => g.GrupaId).ToList();
            CollectionAssert.DoesNotContain(ids, 5);
            CollectionAssert.DoesNotContain(ids, 6);
        }

        [TestMethod]
        public async Task DostupneGrupe_BezFiltera_SortiranoPoDatumuKreiranjaOpadajuce()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.DostupneGrupe(null, null) as ViewResult;

            var ids = (rezultat!.Model as List<CitalackaGrupa>)!.Select(g => g.GrupaId).ToList();
            CollectionAssert.AreEqual(new List<int> { 4, 2, 3, 1 }, ids);
        }

        [TestMethod]
        public async Task DostupneGrupe_ViewBagRegijeIZanrovi_JedinstveniISortirani()
        {
            var kontroler = NapraviKontroler(_korisnik);

            await kontroler.DostupneGrupe("Sarajevo", "Krimi");

            CollectionAssert.AreEqual(new List<string> { "Banja Luka", "Mostar", "Sarajevo" }, (List<string>)kontroler.ViewBag.Regije);
            CollectionAssert.AreEqual(new List<string> { "Fantastika", "Krimi", "Roman" }, (List<string>)kontroler.ViewBag.Zanrovi);
            Assert.AreEqual("Sarajevo", kontroler.ViewBag.OdabranaRegija);
            Assert.AreEqual("Krimi", kontroler.ViewBag.OdabraniZanr);
        }

        // ---------------- PridruziSe ----------------

        [TestMethod]
        public async Task PridruziSe_NeprijavljenKorisnik_PreusmjeravaNaLogin()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.PridruziSe(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
        }

        [TestMethod]
        public async Task PridruziSe_NepostojecaGrupa_VracaNotFound()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.PridruziSe(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task PridruziSe_KorisnikVecClan_NeDodajeNovoClanstvo()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.PridruziSe(5) as RedirectToActionResult;

            Assert.AreEqual("DostupneGrupe", rezultat!.ActionName);
            Assert.AreEqual(1, _kontekst.ClanstvaGrupe.Count());
            Assert.AreEqual(0, _kontekst.Notifikacije.Count());
        }

        [TestMethod]
        public async Task PridruziSe_ZahtjevVecNaCekanju_NeDodajeNoviZahtjev()
        {
            var kontroler = NapraviKontroler(_korisnik);

            await kontroler.PridruziSe(6);

            Assert.AreEqual(1, _kontekst.ZahtjeviZaPristup.Count());
            Assert.AreEqual(0, _kontekst.Notifikacije.Count());
        }

        [TestMethod]
        [DynamicData(nameof(PridruzivanjaIzXml), DynamicDataSourceType.Method)]
        public async Task PridruziSe_TipGrupeIzXml_KreiraOcekivaneZapise(TipGrupe tip, int ocekivanoClanstava, int ocekivanoZahtjeva, int ocekivanoNotifikacija)
        {
            // korisnik je administrator, a postoji jos jedan administrator i jedan obicni korisnik
            var admin = TestHelper.NoviKorisnik("admin-pridruzivanje", Uloga.administrator);
            _kontekst.Korisnici.AddRange(admin, TestHelper.NoviKorisnik("obicni"), TestHelper.NoviKorisnik("drugi-admin", Uloga.administrator));
            _kontekst.CitalackeGrupe.Add(new CitalackaGrupa { GrupaId = 100, Naziv = "Nova grupa", TipGrupe = tip });
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler(admin);

            await kontroler.PridruziSe(100);

            Assert.AreEqual(ocekivanoClanstava, _kontekst.ClanstvaGrupe.Count(c => c.GrupaId == 100));
            Assert.AreEqual(ocekivanoZahtjeva, _kontekst.ZahtjeviZaPristup.Count(z => z.GrupaId == 100));
            Assert.AreEqual(ocekivanoNotifikacija, _kontekst.Notifikacije.Count());
        }

        [TestMethod]
        public async Task PridruziSe_JavnaGrupa_ClanstvoImaStatusClanIPorukuOUspjehu()
        {
            var kontroler = NapraviKontroler(_korisnik);

            await kontroler.PridruziSe(1);

            var clanstvo = _kontekst.ClanstvaGrupe.Single(c => c.GrupaId == 1);
            var notifikacija = _kontekst.Notifikacije.Single();
            Assert.AreEqual(StatusClanstva.clan, clanstvo.StatusClanstva);
            Assert.AreEqual("Uspješno ste se pridružili grupi \"Krimi SA\".", notifikacija.Poruka);
            Assert.IsFalse(notifikacija.Procitana);
        }

        [TestMethod]
        public async Task PridruziSe_PrivatnaGrupa_AdminDobijaObavijestOZahtjevu()
        {
            _kontekst.Korisnici.Add(TestHelper.NoviKorisnik("admin1", Uloga.administrator));
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler(_korisnik);

            await kontroler.PridruziSe(3);

            var zahtjev = _kontekst.ZahtjeviZaPristup.Single(z => z.GrupaId == 3);
            var adminova = _kontekst.Notifikacije.Single(n => n.KorisnikId == "admin1");
            Assert.AreEqual(StatusZahtjeva.na_cekanju, zahtjev.StatusZahtjeva);
            Assert.AreEqual("Novi zahtjev za pristup privatnoj grupi \"Roman MO\".", adminova.Poruka);
        }

        // ---------------- Details ----------------

        [TestMethod]
        public async Task Details_IdNull_VracaNotFound()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Details(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_NepostojecaGrupa_VracaNotFound()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Details(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojecaGrupa_VracaGrupuIObjaveOdNajnovije()
        {
            _kontekst.SadrzajiGrupe.AddRange(
                new SadrzajGrupe { SadrzajId = 1, GrupaId = 1, AutorId = KorisnikId, Naslov = "Starija", DatumObjave = new DateTime(2026, 1, 1) },
                new SadrzajGrupe { SadrzajId = 2, GrupaId = 1, AutorId = KorisnikId, Naslov = "Novija", DatumObjave = new DateTime(2026, 2, 1) },
                new SadrzajGrupe { SadrzajId = 3, GrupaId = 2, AutorId = KorisnikId, Naslov = "Druga grupa", DatumObjave = new DateTime(2026, 3, 1) });
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Details(1) as ViewResult;

            var objave = (List<SadrzajGrupe>)kontroler.ViewBag.Objave;
            Assert.AreEqual(1, ((CitalackaGrupa)rezultat!.Model!).GrupaId);
            CollectionAssert.AreEqual(new List<string> { "Novija", "Starija" }, objave.Select(o => o.Naslov).ToList());
        }

        // ---------------- Create ----------------

        [TestMethod]
        public void Create_Get_VracaPrazanView()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public async Task Create_ValidnaGrupa_SpremaSaDanasnjimDatumomIPreusmjerava()
        {
            var kontroler = NapraviKontroler(_korisnik);
            var nova = new CitalackaGrupa { GrupaId = 50, Naziv = "Nova", Regija = "Zenica", Zanr = "Drama", MaksimalanBrojClanova = 20 };

            var rezultat = await kontroler.Create(nova) as RedirectToActionResult;

            var spremljena = _kontekst.CitalackeGrupe.Single(g => g.GrupaId == 50);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(DateTime.Today, spremljena.DatumKreiranja.Date);
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaViewIstimModelomINeSprema()
        {
            var kontroler = NapraviKontroler(_korisnik);
            kontroler.ModelState.AddModelError("Naziv", "Naziv je obavezan");
            var nova = new CitalackaGrupa { GrupaId = 51 };

            var rezultat = await kontroler.Create(nova) as ViewResult;

            Assert.AreSame(nova, rezultat!.Model);
            Assert.AreEqual(6, _kontekst.CitalackeGrupe.Count());
        }

        // ---------------- Edit ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Edit_Get_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Edit(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_PostojecaGrupa_VracaViewSaGrupom()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Edit(2) as ViewResult;

            Assert.AreEqual("Fantastika SA", ((CitalackaGrupa)rezultat!.Model!).Naziv);
        }

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Edit(1, new CitalackaGrupa { GrupaId = 2 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_ValidnaIzmjena_AzuriraNazivIPreusmjerava()
        {
            var kontroler = NapraviKontroler(_korisnik);
            var izmijenjena = new CitalackaGrupa { GrupaId = 1, Naziv = "Krimi Sarajevo", Regija = "Sarajevo", Zanr = "Krimi" };

            var rezultat = await kontroler.Edit(1, izmijenjena) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual("Krimi Sarajevo", provjera.CitalackeGrupe.Single(g => g.GrupaId == 1).Naziv);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            var kontroler = NapraviKontroler(_korisnik);
            kontroler.ModelState.AddModelError("Naziv", "Naziv je obavezan");
            var izmijenjena = new CitalackaGrupa { GrupaId = 1 };

            var rezultat = await kontroler.Edit(1, izmijenjena) as ViewResult;

            Assert.AreSame(izmijenjena, rezultat!.Model);
        }

        [TestMethod]
        public async Task Edit_Post_GrupaUMedjuvremenuObrisana_VracaNotFound()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Edit(777, new CitalackaGrupa { GrupaId = 777, Naziv = "Ne postoji" });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnaIzmjenaPostojeceGrupe_BacaDbUpdateConcurrencyException()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(
                new CitalackaGrupaController(stubKontekst, TestHelper.UserManagerMock(_korisnik).Object), KorisnikId);

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(1, new CitalackaGrupa { GrupaId = 1, Naziv = "Izmjena" }));
        }

        // ---------------- Delete ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Delete_Get_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_Get_PostojecaGrupa_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.Delete(3) as ViewResult;

            Assert.AreEqual(3, ((CitalackaGrupa)rezultat!.Model!).GrupaId);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojecaGrupa_BriseGrupu()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.DeleteConfirmed(4) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.IsFalse(_kontekst.CitalackeGrupe.Any(g => g.GrupaId == 4));
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaGrupa_NistaNeBriseIPreusmjerava()
        {
            var kontroler = NapraviKontroler(_korisnik);

            var rezultat = await kontroler.DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(6, _kontekst.CitalackeGrupe.Count());
        }
    }
}
