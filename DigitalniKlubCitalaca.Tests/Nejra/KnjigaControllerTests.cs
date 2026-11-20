using System.Xml.Linq;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Services;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Nejra
{
    /// <summary>
    /// Autorica: Nejra Mašović
    /// Testirana klasa: KnjigaController
    /// Zamjenski objekat: IPdfStorageService (vanjsko skladiste za PDF knjige jos nije implementirano)
    /// </summary>
    [TestClass]
    public class KnjigaControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private ApplicationDbContext _kontekst = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestHelper.NoveOpcije();

            using (var seed = TestHelper.NoviKontekst(_opcije))
            {
                seed.Korisnici.AddRange(
                    TestHelper.NoviKorisnik("citalac"),
                    TestHelper.NoviKorisnik("moderator", Uloga.moderator),
                    TestHelper.NoviKorisnik("admin", Uloga.administrator));
                seed.Knjige.AddRange(
                    new Knjiga { KnjigaId = 1, Naziv = "Na Drini ćuprija", Autor = "Ivo Andrić", Zanr = "Roman", PdfPutanja = "/biblioteka/drina.pdf" },
                    new Knjiga { KnjigaId = 2, Naziv = "Derviš i smrt", Autor = "Meša Selimović", Zanr = "Roman", PdfPutanja = "/biblioteka/dervis.pdf" },
                    new Knjiga { KnjigaId = 3, Naziv = "Ubistvo u Orient ekspresu", Autor = "Agatha Christie", Zanr = "Krimi", PdfPutanja = "/biblioteka/orient.pdf" },
                    new Knjiga { KnjigaId = 4, Naziv = "Hobit", Autor = "J. R. R. Tolkien", Zanr = "Fantastika", PdfPutanja = "" },
                    new Knjiga { KnjigaId = 5, Naziv = "Tvrđava", Autor = "Meša Selimović", Zanr = "Roman", PdfPutanja = "/biblioteka/tvrdjava.pdf" });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            // brisu se samo PDF-ovi koje su testovi sami kreirali (lokalno spremanje)
            foreach (var putanja in _kontekst.Knjige.Select(k => k.PdfPutanja).ToList()
                         .Select(p => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", p.TrimStart('/')))
                         .Where(p => File.Exists(p) && Path.GetFileName(p).Length > 30))
            {
                File.Delete(putanja);
            }
            _kontekst.Dispose();
        }

        private KnjigaController NapraviKontroler(string? korisnikId, IPdfStorageService? pdfStorage = null)
        {
            return TestHelper.PripremiKontroler(new KnjigaController(_kontekst, pdfStorage), korisnikId);
        }

        private static IFormFile NapraviPdf(int velicina, string naziv = "knjiga.pdf")
        {
            return new FormFile(new MemoryStream(new byte[velicina]), 0, velicina, "pdfFile", naziv);
        }

        // ---------------- Data driven izvori ----------------

        public static IEnumerable<object[]> ZanroviIzCsv()
        {
            return File.ReadAllLines(TestHelper.PutanjaPodataka("Nejra", "knjige_po_zanru.csv"))
                .Skip(1)
                .Select(red => red.Split(';'))
                .Select(p => new object[] { p[0], int.Parse(p[1]) });
        }

        public static IEnumerable<object?[]> UlogeIzXml()
        {
            return XDocument.Load(TestHelper.PutanjaPodataka("Nejra", "uloge_katalog.xml"))
                .Descendants("korisnik")
                .Select(k => new object?[]
                {
                    string.IsNullOrEmpty((string)k.Attribute("id")!) ? null : (string)k.Attribute("id")!,
                    bool.Parse((string)k.Attribute("ocekivanoAdmin")!)
                });
        }

        // ---------------- Index ----------------

        [TestMethod]
        [DynamicData(nameof(ZanroviIzCsv), DynamicDataSourceType.Method)]
        public async Task Index_FilterZanraIzCsv_VracaOcekivaniBrojKnjiga(string zanr, int ocekivaniBroj)
        {
            var kontroler = NapraviKontroler("citalac");

            var rezultat = await kontroler.Index(zanr) as ViewResult;

            Assert.AreEqual(ocekivaniBroj, ((List<Knjiga>)rezultat!.Model!).Count);
        }

        [TestMethod]
        [DynamicData(nameof(UlogeIzXml), DynamicDataSourceType.Method)]
        public async Task Index_KorisnikIzXml_AdminOpcijeSamoZaAdministratora(string? korisnikId, bool ocekivanoAdmin)
        {
            var kontroler = NapraviKontroler(korisnikId);

            await kontroler.Index(null);

            Assert.AreEqual(ocekivanoAdmin, (bool)kontroler.ViewBag.JeAdministrator);
        }

        [TestMethod]
        public async Task Index_BezFiltera_KnjigeSortiraneAbecedno()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.Index(null) as ViewResult;

            var nazivi = ((List<Knjiga>)rezultat!.Model!).Select(k => k.Naziv).ToList();
            CollectionAssert.AreEqual(
                new List<string> { "Derviš i smrt", "Hobit", "Na Drini ćuprija", "Tvrđava", "Ubistvo u Orient ekspresu" }, nazivi);
        }

        [TestMethod]
        public async Task Index_FilterRoman_ZanroviZaPadajucuListuSviIBezDuplikata()
        {
            var kontroler = NapraviKontroler(null);

            await kontroler.Index("Roman");

            CollectionAssert.AreEqual(new List<string> { "Fantastika", "Krimi", "Roman" }, (List<string>)kontroler.ViewBag.Zanrovi);
            Assert.AreEqual("Roman", kontroler.ViewBag.OdabraniZanr);
        }

        // ---------------- Details / Citaj ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Details_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("citalac");

            var rezultat = await kontroler.Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojecaKnjigaZaAdmina_VracaKnjiguIAdminOpcije()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Details(2) as ViewResult;

            Assert.AreEqual("Meša Selimović", ((Knjiga)rezultat!.Model!).Autor);
            Assert.IsTrue((bool)kontroler.ViewBag.JeAdministrator);
        }

        [TestMethod]
        [DataRow(999)]
        [DataRow(4)]
        public async Task Citaj_NepostojecaKnjigaIliBezPdfa_VracaNotFound(int id)
        {
            var kontroler = NapraviKontroler("citalac");

            var rezultat = await kontroler.Citaj(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Citaj_KnjigaSaPdfom_PreusmjeravaNaPdf()
        {
            var kontroler = NapraviKontroler("citalac");

            var rezultat = await kontroler.Citaj(1) as RedirectResult;

            Assert.AreEqual("/biblioteka/drina.pdf", rezultat!.Url);
        }

        // ---------------- Kontrola pristupa (samo administrator) ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("citalac")]
        [DataRow("moderator")]
        public async Task Create_Get_NijeAdministrator_VracaForbid(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
        }

        [TestMethod]
        public async Task Create_Post_NijeAdministrator_VracaForbidINeSprema()
        {
            var kontroler = NapraviKontroler("moderator");

            var rezultat = await kontroler.Create(new Knjiga { KnjigaId = 6, Naziv = "X" }, NapraviPdf(10));

            Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
            Assert.AreEqual(5, _kontekst.Knjige.Count());
        }

        [TestMethod]
        public async Task Edit_NijeAdministrator_ObjeAkcijeVracajuForbid()
        {
            var kontroler = NapraviKontroler("citalac");

            var get = await kontroler.Edit(1);
            var post = await kontroler.Edit(1, new Knjiga { KnjigaId = 1 }, null);

            Assert.IsInstanceOfType(get, typeof(ForbidResult));
            Assert.IsInstanceOfType(post, typeof(ForbidResult));
        }

        [TestMethod]
        public async Task Delete_NijeAdministrator_KnjigaOstaje()
        {
            var kontroler = NapraviKontroler("citalac");

            var get = await kontroler.Delete(1);
            var post = await kontroler.DeleteConfirmed(1);

            Assert.IsInstanceOfType(get, typeof(ForbidResult));
            Assert.IsInstanceOfType(post, typeof(ForbidResult));
            Assert.AreEqual(5, _kontekst.Knjige.Count());
        }

        // ---------------- Create ----------------

        [TestMethod]
        public async Task Create_Get_Administrator_VracaView()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public async Task Create_Post_BezPdfa_GreskaNaFormi()
        {
            var kontroler = NapraviKontroler("admin");
            var knjiga = new Knjiga { KnjigaId = 6, Naziv = "Bez PDF-a" };

            var rezultat = await kontroler.Create(knjiga, null) as ViewResult;

            Assert.AreSame(knjiga, rezultat!.Model);
            Assert.AreEqual("Morate odabrati PDF knjige.", kontroler.ModelState["pdfFile"]!.Errors.Single().ErrorMessage);
            Assert.AreEqual(5, _kontekst.Knjige.Count());
        }

        [TestMethod]
        public async Task Create_Post_PrazanPdfOd0Bajtova_GreskaNaFormi()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Create(new Knjiga { KnjigaId = 6, Naziv = "Prazan PDF" }, NapraviPdf(0));

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
            Assert.IsTrue(kontroler.ModelState.ContainsKey("pdfFile"));
        }

        [TestMethod]
        public async Task Create_Post_BezSkladista_PdfSeSpremaLokalnoUBiblioteku()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Create(new Knjiga { KnjigaId = 6, Naziv = "Prokleta avlija", Zanr = "Roman" }, NapraviPdf(256)) as RedirectToActionResult;

            var putanja = _kontekst.Knjige.Single(k => k.KnjigaId == 6).PdfPutanja;
            Assert.AreEqual("Index", rezultat!.ActionName);
            StringAssert.StartsWith(putanja, "/biblioteka/");
            StringAssert.EndsWith(putanja, ".pdf");
            Assert.AreEqual(256, new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", putanja.TrimStart('/'))).Length);
        }

        // ---------------- Zamjenski objekat: IPdfStorageService (mock) ----------------

        [TestMethod]
        public async Task Create_Post_SaSkladistem_PutanjaIzSkladistaIFajlProslijedjenJednom()
        {
            var pdf = NapraviPdf(100, "avlija.pdf");
            var skladiste = new Mock<IPdfStorageService>();
            skladiste.Setup(s => s.SpremiAsync(pdf)).ReturnsAsync("https://cdn.dkc.ba/knjige/avlija.pdf");
            var kontroler = NapraviKontroler("admin", skladiste.Object);

            await kontroler.Create(new Knjiga { KnjigaId = 6, Naziv = "Prokleta avlija" }, pdf);

            Assert.AreEqual("https://cdn.dkc.ba/knjige/avlija.pdf", _kontekst.Knjige.Single(k => k.KnjigaId == 6).PdfPutanja);
            skladiste.Verify(s => s.SpremiAsync(pdf), Times.Once);
        }

        [TestMethod]
        public async Task Create_Post_NevalidnaForma_SkladisteSeNePoziva()
        {
            var skladiste = new Mock<IPdfStorageService>();
            var kontroler = NapraviKontroler("admin", skladiste.Object);
            kontroler.ModelState.AddModelError("Naziv", "Obavezno");

            await kontroler.Create(new Knjiga { KnjigaId = 6 }, NapraviPdf(100));

            skladiste.Verify(s => s.SpremiAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [TestMethod]
        public async Task Create_Post_SkladisteNedostupno_IzuzetakIKnjigaNijeDodana()
        {
            var skladiste = new Mock<IPdfStorageService>();
            skladiste.Setup(s => s.SpremiAsync(It.IsAny<IFormFile>())).ThrowsAsync(new IOException("Cloud skladište ne odgovara"));
            var kontroler = NapraviKontroler("admin", skladiste.Object);

            await Assert.ThrowsExceptionAsync<IOException>(() => kontroler.Create(new Knjiga { KnjigaId = 6, Naziv = "X" }, NapraviPdf(100)));

            Assert.AreEqual(5, _kontekst.Knjige.Count());
        }

        [TestMethod]
        public async Task Edit_Post_NoviPdfSaSkladistem_ZamjenjujeStaruPutanju()
        {
            var skladiste = new Mock<IPdfStorageService>();
            skladiste.Setup(s => s.SpremiAsync(It.IsAny<IFormFile>())).ReturnsAsync("https://cdn.dkc.ba/knjige/drina-v2.pdf");
            var kontroler = NapraviKontroler("admin", skladiste.Object);

            await kontroler.Edit(1, new Knjiga { KnjigaId = 1, Naziv = "Na Drini ćuprija", Zanr = "Roman" }, NapraviPdf(50));

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("https://cdn.dkc.ba/knjige/drina-v2.pdf", provjera.Knjige.Single(k => k.KnjigaId == 1).PdfPutanja);
        }

        // ---------------- Edit ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Edit_Get_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Edit(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_PostojecaKnjiga_VracaView()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Edit(3) as ViewResult;

            Assert.AreEqual("Krimi", ((Knjiga)rezultat!.Model!).Zanr);
        }

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapaIliKnjigaNePostoji_VracaNotFound()
        {
            var kontroler = NapraviKontroler("admin");

            var razlicitId = await kontroler.Edit(1, new Knjiga { KnjigaId = 2 }, null);
            var nepostojeca = await kontroler.Edit(999, new Knjiga { KnjigaId = 999 }, null);

            Assert.IsInstanceOfType(razlicitId, typeof(NotFoundResult));
            Assert.IsInstanceOfType(nepostojeca, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_BezNovogPdfa_ZadrzavaStaruPutanjuIakoFormaSaljeDrugu()
        {
            var kontroler = NapraviKontroler("admin");
            var izmjena = new Knjiga { KnjigaId = 1, Naziv = "Na Drini ćuprija (izmijenjeno)", Zanr = "Roman", PdfPutanja = "/hakovano.pdf" };

            var rezultat = await kontroler.Edit(1, izmjena, null) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            var knjiga = provjera.Knjige.Single(k => k.KnjigaId == 1);
            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.AreEqual("Na Drini ćuprija (izmijenjeno)", knjiga.Naziv);
            Assert.AreEqual("/biblioteka/drina.pdf", knjiga.PdfPutanja);
        }

        [TestMethod]
        public async Task Edit_Post_PrazanNoviPdf_ZadrzavaStaruPutanju()
        {
            var kontroler = NapraviKontroler("admin");

            await kontroler.Edit(5, new Knjiga { KnjigaId = 5, Naziv = "Tvrđava", Zanr = "Roman" }, NapraviPdf(0));

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("/biblioteka/tvrdjava.pdf", provjera.Knjige.Single(k => k.KnjigaId == 5).PdfPutanja);
        }

        [TestMethod]
        public async Task Edit_Post_NoviPdfBezSkladista_SpremaSeLokalno()
        {
            var kontroler = NapraviKontroler("admin");

            await kontroler.Edit(2, new Knjiga { KnjigaId = 2, Naziv = "Derviš i smrt", Zanr = "Roman" }, NapraviPdf(64));

            var putanja = _kontekst.Knjige.AsNoTracking().Single(k => k.KnjigaId == 2).PdfPutanja;
            Assert.AreNotEqual("/biblioteka/dervis.pdf", putanja);
            Assert.IsTrue(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", putanja.TrimStart('/'))));
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaViewSaStaromPutanjom()
        {
            var kontroler = NapraviKontroler("admin");
            kontroler.ModelState.AddModelError("Naziv", "Obavezno");
            var izmjena = new Knjiga { KnjigaId = 3, Naziv = "" };

            var rezultat = await kontroler.Edit(3, izmjena, null) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
            Assert.AreEqual("/biblioteka/orient.pdf", izmjena.PdfPutanja);
        }

        // ---------------- Delete ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Delete_Get_IdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_Get_PostojecaKnjiga_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Delete(4) as ViewResult;

            Assert.AreEqual("Hobit", ((Knjiga)rezultat!.Model!).Naziv);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojecaKnjiga_BriseSeIzKataloga()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.DeleteConfirmed(4) as RedirectToActionResult;

            Assert.AreEqual("Index", rezultat!.ActionName);
            Assert.IsFalse(_kontekst.Knjige.Any(k => k.KnjigaId == 4));
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaKnjiga_PreusmjeravaBezGreske()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(5, _kontekst.Knjige.Count());
        }
    }
}
