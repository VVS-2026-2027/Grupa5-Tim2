using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Nejra
{
    /// <summary>
    /// Autorica: Nejra Mašović
    /// Testirana klasa: KnjigaGrupaController (knjige dodijeljene citalackim grupama)
    /// </summary>
    [TestClass]
    public class KnjigaGrupaControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.CitalackeGrupe.AddRange(
                    new CitalackaGrupa { GrupaId = 1, Naziv = "Krimi klub" },
                    new CitalackaGrupa { GrupaId = 2, Naziv = "Klasici" });
                seed.Knjige.AddRange(
                    new Knjiga { KnjigaId = 1, Naziv = "Ubistvo u Orient ekspresu" },
                    new Knjiga { KnjigaId = 2, Naziv = "Na Drini ćuprija" },
                    new Knjiga { KnjigaId = 3, Naziv = "Tvrđava" });
                seed.KnjigeGrupe.AddRange(
                    new KnjigaGrupa { KnjigaGrupaId = 1, KnjigaId = 1, GrupaId = 1 },
                    new KnjigaGrupa { KnjigaGrupaId = 2, KnjigaId = 2, GrupaId = 2 });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private KnjigaGrupaController NapraviKontroler()
        {
            return TestHelper.PripremiKontroler(new KnjigaGrupaController(_kontekst), "admin");
        }

        // ---------------- Index / Details ----------------

        [TestMethod]
        public async Task Index_DvijeDodjele_VracaObjeSaKnjigomIGrupom()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Index() as ViewResult;

            var dodjele = (List<KnjigaGrupa>)rezultat!.Model!;
            Assert.AreEqual(2, dodjele.Count);
            Assert.IsTrue(dodjele.All(d => d.Knjiga != null && d.CitalackaGrupa != null));
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
        public async Task Details_PostojecaDodjela_VracaNazivKnjigeIGrupe()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(2) as ViewResult;

            var dodjela = (KnjigaGrupa)rezultat!.Model!;
            Assert.AreEqual("Na Drini ćuprija", dodjela.Knjiga.Naziv);
            Assert.AreEqual("Klasici", dodjela.CitalackaGrupa.Naziv);
        }

        // ---------------- Create ----------------

        [TestMethod]
        public void Create_Get_ListeSadrzeSveKnjigeIGrupe()
        {
            var kontroler = NapraviKontroler();

            kontroler.Create();

            CollectionAssert.AreEquivalent(new List<string> { "1", "2", "3" },
                ((SelectList)kontroler.ViewData["KnjigaId"]!).Select(o => o.Value).ToList());
            Assert.AreEqual(2, ((SelectList)kontroler.ViewData["GrupaId"]!).Count());
        }

        [TestMethod]
        public async Task Create_Post_IspravnaDodjela_KnjigaDodanaGrupi()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Create(new KnjigaGrupa { KnjigaGrupaId = 3, KnjigaId = 3, GrupaId = 2 }) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            CollectionAssert.AreEquivalent(new List<int> { 2, 3 },
                _kontekst.KnjigeGrupe.Where(k => k.GrupaId == 2).Select(k => k.KnjigaId).ToList());
        }

        [TestMethod]
        public async Task Create_Post_NevalidanModel_VracaViewSaOznacenimVrijednostima()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("KnjigaId", "Obavezno");
            var dodjela = new KnjigaGrupa { KnjigaGrupaId = 3, KnjigaId = 3, GrupaId = 1 };

            var rezultat = await kontroler.Create(dodjela) as ViewResult;

            Assert.AreSame(dodjela, rezultat!.Model);
            Assert.AreEqual(3, ((SelectList)kontroler.ViewData["KnjigaId"]!).SelectedValue);
            Assert.AreEqual(2, _kontekst.KnjigeGrupe.Count());
        }

        [TestMethod]
        public async Task Create_Post_IstaKnjigaUIstojGrupi_DuplikatSeSprema()
        {
            // dokumentuje nedostatak: nema provjere da je knjiga vec dodijeljena toj grupi
            var kontroler = NapraviKontroler();

            await kontroler.Create(new KnjigaGrupa { KnjigaGrupaId = 3, KnjigaId = 1, GrupaId = 1 });

            Assert.AreEqual(2, _kontekst.KnjigeGrupe.Count(k => k.KnjigaId == 1 && k.GrupaId == 1));
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
        public async Task Edit_Get_PostojecaDodjela_ListeOznacavajuTrenutnuKnjiguIGrupu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1) as ViewResult;

            Assert.AreEqual(1, ((KnjigaGrupa)rezultat!.Model!).KnjigaGrupaId);
            Assert.AreEqual(1, ((SelectList)kontroler.ViewData["KnjigaId"]!).SelectedValue);
            Assert.AreEqual(1, ((SelectList)kontroler.ViewData["GrupaId"]!).SelectedValue);
        }

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1, new KnjigaGrupa { KnjigaGrupaId = 2 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_PremjestanjeKnjigeUDruguGrupu_Spremljeno()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1, new KnjigaGrupa { KnjigaGrupaId = 1, KnjigaId = 1, GrupaId = 2 }) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(2, provjera.KnjigeGrupe.Single(k => k.KnjigaGrupaId == 1).GrupaId);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("GrupaId", "Obavezno");
            var izmjena = new KnjigaGrupa { KnjigaGrupaId = 1, KnjigaId = 1, GrupaId = 1 };

            var rezultat = await kontroler.Edit(1, izmjena) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
            Assert.IsNotNull(kontroler.ViewData["GrupaId"]);
        }

        [TestMethod]
        public async Task Edit_Post_DodjelaObrisanaUMedjuvremenu_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(77, new KnjigaGrupa { KnjigaGrupaId = 77, KnjigaId = 1, GrupaId = 1 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnaIzmjena_BacaDbUpdateConcurrencyException()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(new KnjigaGrupaController(stubKontekst), "admin");

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(1, new KnjigaGrupa { KnjigaGrupaId = 1, KnjigaId = 1, GrupaId = 2 }));
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
        public async Task Delete_PostojecaDodjela_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(1) as ViewResult;

            Assert.AreEqual("Krimi klub", ((KnjigaGrupa)rezultat!.Model!).CitalackaGrupa.Naziv);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojecaDodjela_KnjigaUklonjenaIzGrupeAliOstajeUKatalogu()
        {
            var kontroler = NapraviKontroler();

            await kontroler.DeleteConfirmed(1);

            Assert.IsFalse(_kontekst.KnjigeGrupe.Any(k => k.KnjigaGrupaId == 1));
            Assert.AreEqual(3, _kontekst.Knjige.Count());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaDodjela_NistaNeBrise()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.DeleteConfirmed(999) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(2, _kontekst.KnjigeGrupe.Count());
        }
    }
}
