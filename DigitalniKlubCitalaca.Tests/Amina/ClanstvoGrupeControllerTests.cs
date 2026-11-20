using System.Text.Json;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Amina
{
    /// <summary>
    /// Autorica: Amina Peštalić
    /// Testirana klasa: ClanstvoGrupeController
    /// </summary>
    [TestClass]
    public class ClanstvoGrupeControllerTests
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
                    new CitalackaGrupa { GrupaId = 1, Naziv = "Krimi klub" },
                    new CitalackaGrupa { GrupaId = 2, Naziv = "Poezija" });
                seed.ClanstvaGrupe.AddRange(
                    new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = "k1", GrupaId = 1, StatusClanstva = StatusClanstva.clan, DatumPridruzivanja = new DateTime(2026, 10, 1) },
                    new ClanstvoGrupe { ClanstvoId = 2, KorisnikId = "k2", GrupaId = 1, StatusClanstva = StatusClanstva.ceka_odobrenje, DatumPridruzivanja = new DateTime(2026, 11, 1) });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private ClanstvoGrupeController NapraviKontroler()
        {
            return TestHelper.PripremiKontroler(new ClanstvoGrupeController(_kontekst), "k1");
        }

        // ---------------- Data driven izvor (JSON) ----------------

        public static IEnumerable<object[]> StatusiIzJson()
        {
            using var dokument = JsonDocument.Parse(File.ReadAllText(TestHelper.PutanjaPodataka("Amina", "statusi_clanstva.json")));
            return dokument.RootElement.EnumerateArray()
                .Select(e => new object[]
                {
                    Enum.Parse<StatusClanstva>(e.GetProperty("pocetniStatus").GetString()!),
                    Enum.Parse<StatusClanstva>(e.GetProperty("noviStatus").GetString()!)
                })
                .ToList();
        }

        // ---------------- Index / Details ----------------

        [TestMethod]
        public async Task Index_DvaClanstva_VracaObaSaGrupomIKorisnikom()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Index() as ViewResult;

            var clanstva = (List<ClanstvoGrupe>)rezultat!.Model!;
            Assert.AreEqual(2, clanstva.Count);
            Assert.IsTrue(clanstva.All(c => c.CitalackaGrupa != null && c.Korisnik != null));
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
        public async Task Details_PostojeceClanstvo_VracaClanstvoSaNazivomGrupe()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(2) as ViewResult;

            var clanstvo = (ClanstvoGrupe)rezultat!.Model!;
            Assert.AreEqual("Krimi klub", clanstvo.CitalackaGrupa.Naziv);
            Assert.AreEqual(StatusClanstva.ceka_odobrenje, clanstvo.StatusClanstva);
        }

        // ---------------- Create ----------------

        // Regresioni test: prije ispravke lista je koristila nepostojece svojstvo "KorisnikId" pa su sve opcije bile prazne
        [TestMethod]
        public void Create_Get_ListaKorisnikaImaIdKaoVrijednostIEmailKaoTekst()
        {
            var kontroler = NapraviKontroler();

            kontroler.Create();

            var opcije = ((SelectList)kontroler.ViewData["KorisnikId"]!).ToList();
            CollectionAssert.AreEquivalent(new List<string> { "k1", "k2" }, opcije.Select(o => o.Value).ToList());
            CollectionAssert.AreEquivalent(new List<string> { "k1@test.ba", "k2@test.ba" }, opcije.Select(o => o.Text).ToList());
        }

        [TestMethod]
        public void Create_Get_ListaGrupaSadrziSveGrupe()
        {
            var kontroler = NapraviKontroler();

            var rezultat = kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
            Assert.AreEqual(2, ((SelectList)kontroler.ViewData["GrupaId"]!).Count());
        }

        [TestMethod]
        public async Task Create_Post_IspravnoClanstvo_SpremaIPreusmjerava()
        {
            var kontroler = NapraviKontroler();
            var novo = new ClanstvoGrupe { ClanstvoId = 3, KorisnikId = "k2", GrupaId = 2, StatusClanstva = StatusClanstva.clan };

            var rezultat = await kontroler.Create(novo) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(1, _kontekst.ClanstvaGrupe.Count(c => c.GrupaId == 2));
        }

        [TestMethod]
        public async Task Create_Post_NevalidanModel_VracaViewSaOdabranimKorisnikom()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("GrupaId", "Obavezno");
            var novo = new ClanstvoGrupe { ClanstvoId = 3, KorisnikId = "k2" };

            var rezultat = await kontroler.Create(novo) as ViewResult;

            var lista = (SelectList)kontroler.ViewData["KorisnikId"]!;
            Assert.AreSame(novo, rezultat!.Model);
            Assert.AreEqual("k2", lista.SelectedValue);
            Assert.AreEqual(2, _kontekst.ClanstvaGrupe.Count());
        }

        [TestMethod]
        public async Task Create_Post_IstiKorisnikIstaGrupa_DuplikatSeSprema()
        {
            // dokumentuje nedostatak: kontroler ne provjerava da li je korisnik vec clan grupe
            var kontroler = NapraviKontroler();

            await kontroler.Create(new ClanstvoGrupe { ClanstvoId = 3, KorisnikId = "k1", GrupaId = 1 });

            Assert.AreEqual(2, _kontekst.ClanstvaGrupe.Count(c => c.KorisnikId == "k1" && c.GrupaId == 1));
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
        public async Task Edit_Get_PostojeceClanstvo_ListeImajuOznacenuTrenutnuVrijednost()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(2) as ViewResult;

            Assert.AreEqual(2, ((ClanstvoGrupe)rezultat!.Model!).ClanstvoId);
            Assert.AreEqual("k2", ((SelectList)kontroler.ViewData["KorisnikId"]!).SelectedValue);
            Assert.AreEqual(1, ((SelectList)kontroler.ViewData["GrupaId"]!).SelectedValue);
        }

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1, new ClanstvoGrupe { ClanstvoId = 2 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        [DynamicData(nameof(StatusiIzJson), DynamicDataSourceType.Method)]
        public async Task Edit_Post_PromjenaStatusaIzJson_NoviStatusSpremljen(StatusClanstva pocetni, StatusClanstva novi)
        {
            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.ClanstvaGrupe.Add(new ClanstvoGrupe { ClanstvoId = 10, KorisnikId = "k2", GrupaId = 2, StatusClanstva = pocetni });
                seed.SaveChanges();
            }
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(10, new ClanstvoGrupe { ClanstvoId = 10, KorisnikId = "k2", GrupaId = 2, StatusClanstva = novi }) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(novi, provjera.ClanstvaGrupe.Single(c => c.ClanstvoId == 10).StatusClanstva);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("StatusClanstva", "Neispravno");
            var izmjena = new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = "k1", GrupaId = 1 };

            var rezultat = await kontroler.Edit(1, izmjena) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
            Assert.IsNotNull(kontroler.ViewData["GrupaId"]);
        }

        [TestMethod]
        public async Task Edit_Post_ClanstvoObrisanoUMedjuvremenu_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(321, new ClanstvoGrupe { ClanstvoId = 321, KorisnikId = "k1", GrupaId = 1 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnaIzmjena_BacaDbUpdateConcurrencyException()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(new ClanstvoGrupeController(stubKontekst), "k1");

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(1, new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = "k1", GrupaId = 1 }));
        }

        // ---------------- Delete ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Delete_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_PostojeceClanstvo_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(1) as ViewResult;

            Assert.AreEqual("k1", ((ClanstvoGrupe)rezultat!.Model!).KorisnikId);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeceClanstvo_KorisnikNapustaGrupu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.IsFalse(_kontekst.ClanstvaGrupe.Any(c => c.KorisnikId == "k1"));
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeceClanstvo_NistaNeBrise()
        {
            var kontroler = NapraviKontroler();

            await kontroler.DeleteConfirmed(999);

            Assert.AreEqual(2, _kontekst.ClanstvaGrupe.Count());
        }
    }
}
