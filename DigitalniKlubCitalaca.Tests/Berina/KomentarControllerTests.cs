using System.Xml.Linq;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Berina
{
    /// <summary>
    /// Autorica: Berina Šabeta
    /// Testirana klasa: KomentarController
    /// </summary>
    [TestClass]
    public class KomentarControllerTests
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
                seed.CitalackeGrupe.Add(new CitalackaGrupa { GrupaId = 1, Naziv = "Krimi klub" });
                seed.SadrzajiGrupe.Add(new SadrzajGrupe { SadrzajId = 1, AutorId = "k1", GrupaId = 1, Naslov = "Recenzija", TipSadrzaja = TipSadrzaja.clanak });
                seed.Komentari.AddRange(
                    new Komentar { KomentarId = 1, SadrzajId = 1, AutorId = "k2", Tekst = "Super!", StatusKomentara = StatusKomentara.aktivan },
                    new Komentar { KomentarId = 2, SadrzajId = 1, AutorId = "k1", Tekst = "Hvala", StatusKomentara = StatusKomentara.aktivan });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
        }

        private KomentarController NapraviKontroler()
        {
            return TestHelper.PripremiKontroler(new KomentarController(_kontekst), "k1");
        }

        // ---------------- Data driven izvor (XML) ----------------

        public static IEnumerable<object[]> KomentariIzXml()
        {
            return XDocument.Load(TestHelper.PutanjaPodataka("Berina", "komentari_notifikacije.xml"))
                .Descendants("slucaj")
                .Select(s => new object[]
                {
                    (string)s.Element("autorKomentara")!,
                    (int)s.Element("sadrzajId")!,
                    (int)s.Element("ocekivanoNotifikacija")!
                });
        }

        // ---------------- Index / Details ----------------

        [TestMethod]
        public async Task Index_PostojeDvaKomentara_VracaObaSaAutoromIObjavom()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Index() as ViewResult;

            var komentari = (List<Komentar>)rezultat!.Model!;
            Assert.AreEqual(2, komentari.Count);
            Assert.IsTrue(komentari.All(k => k.Autor != null && k.SadrzajGrupe != null));
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
        public async Task Details_PostojeciKomentar_VracaKomentar()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Details(1) as ViewResult;

            Assert.AreEqual("Super!", ((Komentar)rezultat!.Model!).Tekst);
        }

        // ---------------- Create ----------------

        [TestMethod]
        public void Create_Get_PadajuceListeAutoraIObjava()
        {
            var kontroler = NapraviKontroler();

            var rezultat = kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
            Assert.AreEqual(2, ((SelectList)kontroler.ViewData["AutorId"]!).Count());
            Assert.AreEqual(1, ((SelectList)kontroler.ViewData["SadrzajId"]!).Count());
        }

        [TestMethod]
        [DynamicData(nameof(KomentariIzXml), DynamicDataSourceType.Method)]
        public async Task Create_AutorIObjavaIzXml_NotifikacijaSamoZaTudjuObjavu(string autorKomentara, int sadrzajId, int ocekivanoNotifikacija)
        {
            var kontroler = NapraviKontroler();
            var komentar = new Komentar { KomentarId = 50, SadrzajId = sadrzajId, AutorId = autorKomentara, Tekst = "Novi komentar" };

            var rezultat = await kontroler.Create(komentar) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(3, _kontekst.Komentari.Count());
            Assert.AreEqual(ocekivanoNotifikacija, _kontekst.Notifikacije.Count());
        }

        [TestMethod]
        public async Task Create_KomentarNaTudjuObjavu_NotifikacijaIdeAutoruObjave()
        {
            var kontroler = NapraviKontroler();

            await kontroler.Create(new Komentar { KomentarId = 51, SadrzajId = 1, AutorId = "k2", Tekst = "Slažem se" });

            var notifikacija = _kontekst.Notifikacije.Single();
            Assert.AreEqual("k1", notifikacija.KorisnikId);
            Assert.AreEqual("Novi komentar na vašoj objavi.", notifikacija.Poruka);
            Assert.AreEqual("/Komentar/Index", notifikacija.Link);
            Assert.IsFalse(notifikacija.Procitana);
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaViewBezSpremanja()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("Tekst", "Tekst je obavezan");
            var komentar = new Komentar { KomentarId = 52, SadrzajId = 1, AutorId = "k2" };

            var rezultat = await kontroler.Create(komentar) as ViewResult;

            Assert.AreSame(komentar, rezultat!.Model);
            Assert.IsInstanceOfType(kontroler.ViewData["SadrzajId"], typeof(SelectList));
            Assert.AreEqual(2, _kontekst.Komentari.Count());
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
        public async Task Edit_Get_PostojeciKomentar_VracaViewSaListama()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(2) as ViewResult;

            Assert.AreEqual("Hvala", ((Komentar)rezultat!.Model!).Tekst);
            Assert.IsNotNull(kontroler.ViewData["AutorId"]);
        }

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(1, new Komentar { KomentarId = 2 });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        [DataRow(StatusKomentara.obrisan)]
        [DataRow(StatusKomentara.neprikladan)]
        public async Task Edit_Post_PromjenaStatusa_StatusSpremljen(StatusKomentara noviStatus)
        {
            var kontroler = NapraviKontroler();
            var izmjena = new Komentar { KomentarId = 1, SadrzajId = 1, AutorId = "k2", Tekst = "Super!", StatusKomentara = noviStatus };

            var rezultat = await kontroler.Edit(1, izmjena) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual(noviStatus, provjera.Komentari.Single(k => k.KomentarId == 1).StatusKomentara);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaModelom()
        {
            var kontroler = NapraviKontroler();
            kontroler.ModelState.AddModelError("Tekst", "Tekst je obavezan");
            var izmjena = new Komentar { KomentarId = 1, SadrzajId = 1, AutorId = "k2" };

            var rezultat = await kontroler.Edit(1, izmjena) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
        }

        [TestMethod]
        public async Task Edit_Post_KomentarObrisanUMedjuvremenu_VracaNotFound()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Edit(404, new Komentar { KomentarId = 404, SadrzajId = 1, AutorId = "k2" });

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_KonkurentnaIzmjena_BacaDbUpdateConcurrencyException()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(new KomentarController(stubKontekst), "k1");

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(1, new Komentar { KomentarId = 1, SadrzajId = 1, AutorId = "k2", Tekst = "Izmjena" }));
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
        public async Task Delete_PostojeciKomentar_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.Delete(1) as ViewResult;

            Assert.AreEqual(1, ((Komentar)rezultat!.Model!).KomentarId);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciKomentar_BriseSamoTajKomentar()
        {
            var kontroler = NapraviKontroler();

            var rezultat = await kontroler.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            CollectionAssert.AreEqual(new List<int> { 2 }, _kontekst.Komentari.Select(k => k.KomentarId).ToList());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciKomentar_NistaNeBrise()
        {
            var kontroler = NapraviKontroler();

            await kontroler.DeleteConfirmed(999);

            Assert.AreEqual(2, _kontekst.Komentari.Count());
        }
    }
}
