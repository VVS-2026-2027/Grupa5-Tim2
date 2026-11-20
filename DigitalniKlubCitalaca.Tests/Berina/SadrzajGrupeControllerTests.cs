using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Services;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Berina
{
    /// <summary>
    /// Autorica: Berina Šabeta
    /// Testirana klasa: SadrzajGrupeController
    /// Zamjenski objekat: IModeracijaService (automatska moderacija komentara jos nije implementirana)
    /// </summary>
    [TestClass]
    public class SadrzajGrupeControllerTests
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
                seed.Korisnici.AddRange(
                    TestHelper.NoviKorisnik("k1"),
                    TestHelper.NoviKorisnik("k2"),
                    TestHelper.NoviKorisnik("mod", Uloga.moderator),
                    TestHelper.NoviKorisnik("admin", Uloga.administrator));
                seed.CitalackeGrupe.Add(new CitalackaGrupa { GrupaId = 1, Naziv = "Krimi klub", TipGrupe = TipGrupe.javna });
                seed.ClanstvaGrupe.AddRange(
                    new ClanstvoGrupe { ClanstvoId = 1, KorisnikId = "k1", GrupaId = 1 },
                    new ClanstvoGrupe { ClanstvoId = 2, KorisnikId = "k2", GrupaId = 1 },
                    new ClanstvoGrupe { ClanstvoId = 3, KorisnikId = "mod", GrupaId = 1 });
                seed.SadrzajiGrupe.AddRange(
                    new SadrzajGrupe { SadrzajId = 1, AutorId = "k1", GrupaId = 1, Naslov = "Recenzija", TipSadrzaja = TipSadrzaja.clanak, DatumObjave = sada.AddDays(-2) },
                    new SadrzajGrupe { SadrzajId = 2, AutorId = "k1", GrupaId = 1, Naslov = "Istekli story", TipSadrzaja = TipSadrzaja.story, Link = "/s.jpg", DatumObjave = sada.AddHours(-30) },
                    new SadrzajGrupe { SadrzajId = 3, AutorId = "k2", GrupaId = 1, Naslov = "PDF knjiga", TipSadrzaja = TipSadrzaja.pdf, Link = "/uploads/a.pdf", DatumObjave = sada.AddDays(-1) },
                    new SadrzajGrupe { SadrzajId = 4, AutorId = "", GrupaId = 1, Naslov = "Objava bez autora", TipSadrzaja = TipSadrzaja.clanak, DatumObjave = sada });
                seed.Komentari.AddRange(
                    new Komentar { KomentarId = 1, SadrzajId = 1, AutorId = "k2", Tekst = "Stariji", DatumKomentara = sada.AddHours(-5) },
                    new Komentar { KomentarId = 2, SadrzajId = 1, AutorId = "mod", Tekst = "Noviji", DatumKomentara = sada.AddHours(-1) },
                    new Komentar { KomentarId = 3, SadrzajId = 3, AutorId = "k1", Tekst = "Drugi sadrzaj", DatumKomentara = sada });
                seed.SaveChanges();
            }

            _kontekst = TestHelper.NoviKontekst(_opcije);
        }

        [TestCleanup]
        public void Ciscenje()
        {
            _kontekst.Dispose();
            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (Directory.Exists(uploads))
                Directory.Delete(uploads, true);
        }

        private SadrzajGrupeController NapraviKontroler(string? korisnikId, IModeracijaService? moderacija = null)
        {
            return TestHelper.PripremiKontroler(new SadrzajGrupeController(_kontekst, moderacija), korisnikId);
        }

        private static IFormFile NapraviPdf(int velicina)
        {
            return new FormFile(new MemoryStream(new byte[velicina]), 0, velicina, "pdfFile", "knjiga.pdf");
        }

        // ---------------- Data driven izvori ----------------

        public static IEnumerable<object[]> ValidacijaIzCsv()
        {
            return File.ReadAllLines(TestHelper.PutanjaPodataka("Berina", "validacija_objave.csv"))
                .Skip(1)
                .Select(red => red.Split(';'))
                .Select(p => new object[] { Enum.Parse<TipSadrzaja>(p[0]), p[1], int.Parse(p[2]), bool.Parse(p[3]), int.Parse(p[4]) });
        }

        private record SlucajVidljivosti(
            [property: JsonPropertyName("tipSadrzaja")] string TipSadrzaja,
            [property: JsonPropertyName("satiOdObjave")] int SatiOdObjave,
            [property: JsonPropertyName("ocekivanoVidljivo")] bool OcekivanoVidljivo);

        public static IEnumerable<object[]> VidljivostIzJson()
        {
            var json = File.ReadAllText(TestHelper.PutanjaPodataka("Berina", "vidljivost_objava.json"));
            return JsonSerializer.Deserialize<List<SlucajVidljivosti>>(json)!
                .Select(s => new object[] { Enum.Parse<TipSadrzaja>(s.TipSadrzaja), s.SatiOdObjave, s.OcekivanoVidljivo });
        }

        // ---------------- Index ----------------

        [TestMethod]
        [DynamicData(nameof(VidljivostIzJson), DynamicDataSourceType.Method)]
        public async Task Index_TipIStarostIzJson_StoryVidljivSamo24Sata(TipSadrzaja tip, int satiOdObjave, bool ocekivanoVidljivo)
        {
            _kontekst.SadrzajiGrupe.Add(new SadrzajGrupe { SadrzajId = 100, AutorId = "k1", GrupaId = 1, TipSadrzaja = tip, DatumObjave = DateTime.Now.AddHours(-satiOdObjave) });
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Index() as ViewResult;

            var ids = ((List<SadrzajGrupe>)rezultat!.Model!).Select(s => s.SadrzajId).ToList();
            Assert.AreEqual(ocekivanoVidljivo, ids.Contains(100));
        }

        [TestMethod]
        public async Task Index_PostojeceObjave_UcitaniAutorIGrupa()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Index() as ViewResult;

            var objave = (List<SadrzajGrupe>)rezultat!.Model!;
            CollectionAssert.AreEquivalent(new List<int> { 1, 3 }, objave.Select(o => o.SadrzajId).ToList());
            Assert.IsTrue(objave.All(o => o.Autor != null && o.CitalackaGrupa != null));
        }

        // ---------------- Details ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        [DataRow(2)]
        public async Task Details_IdNullNepostojeciIliIstekliStory_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojecaObjava_KomentariSamoTeObjaveOdNajnovijeg()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Details(1) as ViewResult;

            var komentari = (List<Komentar>)kontroler.ViewBag.Komentari;
            Assert.AreEqual(1, ((SadrzajGrupe)rezultat!.Model!).SadrzajId);
            CollectionAssert.AreEqual(new List<string> { "Noviji", "Stariji" }, komentari.Select(k => k.Tekst).ToList());
        }

        [TestMethod]
        [DataRow("mod", true)]
        [DataRow("k2", false)]
        [DataRow("admin", false)]
        [DataRow(null, false)]
        public async Task Details_RazliciteUloge_JeModeratorSamoZaModeratora(string? korisnikId, bool ocekivano)
        {
            var kontroler = NapraviKontroler(korisnikId);

            await kontroler.Details(1);

            Assert.AreEqual(ocekivano, (bool)kontroler.ViewBag.JeModerator);
        }

        // ---------------- DodajKomentar ----------------

        [TestMethod]
        public async Task DodajKomentar_NeprijavljenKorisnik_PreusmjeravaNaLogin()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.DodajKomentar(1, "Tekst");

            Assert.AreEqual("/Account/Login", ((RedirectToPageResult)rezultat).PageName);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        public async Task DodajKomentar_PrazanTekst_NeSpremaKomentar(string? tekst)
        {
            var kontroler = NapraviKontroler("k2");

            var rezultat = await kontroler.DodajKomentar(1, tekst!) as RedirectToActionResult;

            Assert.AreEqual("Details", rezultat!.ActionName);
            Assert.AreEqual(3, _kontekst.Komentari.Count());
        }

        [TestMethod]
        public async Task DodajKomentar_TudjaObjava_SpremaKomentarIObavjestavaAutora()
        {
            var kontroler = NapraviKontroler("k2");

            var rezultat = await kontroler.DodajKomentar(1, "Odlična recenzija!") as RedirectToActionResult;

            var komentar = _kontekst.Komentari.Single(k => k.Tekst == "Odlična recenzija!");
            var notifikacija = _kontekst.Notifikacije.Single();
            Assert.AreEqual(1, rezultat!.RouteValues!["id"]);
            Assert.AreEqual("k2", komentar.AutorId);
            Assert.AreEqual(StatusKomentara.aktivan, komentar.StatusKomentara);
            Assert.AreEqual("k1", notifikacija.KorisnikId);
            Assert.AreEqual("Novi komentar na vašoj objavi u grupi \"Krimi klub\".", notifikacija.Poruka);
        }

        [TestMethod]
        public async Task DodajKomentar_AutorKomentariseSvojuObjavu_BezNotifikacije()
        {
            var kontroler = NapraviKontroler("k1");

            await kontroler.DodajKomentar(1, "Dopuna");

            Assert.AreEqual(4, _kontekst.Komentari.Count());
            Assert.AreEqual(0, _kontekst.Notifikacije.Count());
        }

        [TestMethod]
        [DataRow(4)]
        [DataRow(999)]
        public async Task DodajKomentar_ObjavaBezAutoraIliNepostojeca_KomentarSpremljenBezNotifikacije(int sadrzajId)
        {
            var kontroler = NapraviKontroler("k2");

            await kontroler.DodajKomentar(sadrzajId, "Komentar");

            Assert.AreEqual(1, _kontekst.Komentari.Count(k => k.SadrzajId == sadrzajId));
            Assert.AreEqual(0, _kontekst.Notifikacije.Count());
        }

        // ---------------- Zamjenski objekat: IModeracijaService (mock) ----------------

        [TestMethod]
        public async Task DodajKomentar_ModeracijaOznaciNeprikladan_KomentarImaStatusNeprikladan()
        {
            var moderacija = new Mock<IModeracijaService>();
            moderacija.Setup(m => m.JeNeprikladanAsync("Ružna riječ")).ReturnsAsync(true);
            var kontroler = NapraviKontroler("k2", moderacija.Object);

            await kontroler.DodajKomentar(1, "Ružna riječ");

            Assert.AreEqual(StatusKomentara.neprikladan, _kontekst.Komentari.Single(k => k.Tekst == "Ružna riječ").StatusKomentara);
        }

        [TestMethod]
        public async Task DodajKomentar_ModeracijaPropustiTekst_KomentarAktivanIModeracijaPozvanaJednom()
        {
            var moderacija = new Mock<IModeracijaService>();
            moderacija.Setup(m => m.JeNeprikladanAsync(It.IsAny<string>())).ReturnsAsync(false);
            var kontroler = NapraviKontroler("k2", moderacija.Object);

            await kontroler.DodajKomentar(1, "Pristojan komentar");

            Assert.AreEqual(StatusKomentara.aktivan, _kontekst.Komentari.Single(k => k.Tekst == "Pristojan komentar").StatusKomentara);
            moderacija.Verify(m => m.JeNeprikladanAsync("Pristojan komentar"), Times.Once);
        }

        [TestMethod]
        public async Task DodajKomentar_PrazanTekst_ModeracijaSeNePoziva()
        {
            var moderacija = new Mock<IModeracijaService>();
            var kontroler = NapraviKontroler("k2", moderacija.Object);

            await kontroler.DodajKomentar(1, "  ");

            moderacija.Verify(m => m.JeNeprikladanAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task DodajKomentar_ModeracijaNedostupna_IzuzetakIKomentarNijeSpremljen()
        {
            var moderacija = new Mock<IModeracijaService>();
            moderacija.Setup(m => m.JeNeprikladanAsync(It.IsAny<string>())).ThrowsAsync(new HttpRequestException("Servis za moderaciju ne odgovara"));
            var kontroler = NapraviKontroler("k2", moderacija.Object);

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => kontroler.DodajKomentar(1, "Komentar"));

            Assert.AreEqual(3, _kontekst.Komentari.Count());
        }

        // ---------------- Create (GET) ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("nepostojeci")]
        public async Task Create_Get_NepoznatKorisnik_PreusmjeravaNaAccessDenied(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.Create(1) as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
            Assert.AreEqual("Home", rezultat.ControllerName);
        }

        [TestMethod]
        public async Task Create_Get_Administrator_ZabranjenoUzPorukuOGresci()
        {
            var kontroler = NapraviKontroler("admin");

            var rezultat = await kontroler.Create(1) as RedirectToActionResult;

            Assert.AreEqual("CitalackaGrupa", rezultat!.ControllerName);
            Assert.AreEqual("Administrator ne može objavljivati sadržaj u čitalačkim grupama.", kontroler.TempData["Greska"]);
        }

        [TestMethod]
        public async Task Create_Get_ObicanKorisnik_VracaNovuObjavuZaGrupu()
        {
            var kontroler = NapraviKontroler("k2");

            var rezultat = await kontroler.Create(1) as ViewResult;

            var objava = (SadrzajGrupe)rezultat!.Model!;
            Assert.AreEqual(1, objava.GrupaId);
            Assert.AreEqual(DateTime.Today, objava.DatumObjave.Date);
        }

        // ---------------- Create (POST) ----------------

        [TestMethod]
        public async Task Create_Post_NepoznatKorisnik_PreusmjeravaNaAccessDenied()
        {
            var kontroler = NapraviKontroler(null);

            var rezultat = await kontroler.Create(new SadrzajGrupe { GrupaId = 1 }, null) as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
        }

        [TestMethod]
        public async Task Create_Post_Administrator_NeSpremaObjavu()
        {
            var kontroler = NapraviKontroler("admin");

            await kontroler.Create(new SadrzajGrupe { GrupaId = 1, Naslov = "Admin objava", TipSadrzaja = TipSadrzaja.clanak }, null);

            Assert.IsNotNull(kontroler.TempData["Greska"]);
            Assert.AreEqual(4, _kontekst.SadrzajiGrupe.Count());
        }

        [TestMethod]
        [DynamicData(nameof(ValidacijaIzCsv), DynamicDataSourceType.Method)]
        public async Task Create_Post_ValidacijaIzCsv_SpremaSamoIspravneObjave(TipSadrzaja tip, string link, int velicinaPdf, bool ocekivanoValidno, int ocekivanoNovih)
        {
            var kontroler = NapraviKontroler("k2");
            var objava = new SadrzajGrupe { GrupaId = 1, Naslov = "Nova", TipSadrzaja = tip, Link = link };

            await kontroler.Create(objava, NapraviPdf(velicinaPdf));

            Assert.AreEqual(ocekivanoValidno, kontroler.ModelState.IsValid);
            Assert.AreEqual(4 + ocekivanoNovih, _kontekst.SadrzajiGrupe.Count());
        }

        [TestMethod]
        public async Task Create_Post_IspravanClanak_PostavljaAutoraIObavjestavaOstaleClanove()
        {
            var kontroler = NapraviKontroler("k2");
            var objava = new SadrzajGrupe { GrupaId = 1, Naslov = "Novi clanak", TipSadrzaja = TipSadrzaja.clanak, Link = null!, Opis = null };

            var rezultat = await kontroler.Create(objava, null) as RedirectToActionResult;

            var spremljena = _kontekst.SadrzajiGrupe.Single(s => s.Naslov == "Novi clanak");
            var primaoci = _kontekst.Notifikacije.Select(n => n.KorisnikId).ToList();
            Assert.AreEqual("Details", rezultat!.ActionName);
            Assert.AreEqual("k2", spremljena.AutorId);
            Assert.AreEqual(string.Empty, spremljena.Link);
            Assert.AreEqual(string.Empty, spremljena.Opis);
            CollectionAssert.AreEquivalent(new List<string> { "k1", "mod" }, primaoci);
            Assert.IsTrue(_kontekst.Notifikacije.All(n => n.Poruka == "Nova objava u grupi \"Krimi klub\"."));
        }

        [TestMethod]
        public async Task Create_Post_Story_PorukaONovomStoryju()
        {
            var kontroler = NapraviKontroler("k1");

            await kontroler.Create(new SadrzajGrupe { GrupaId = 1, Naslov = "Moj story", TipSadrzaja = TipSadrzaja.story, Link = "/s2.jpg" }, null);

            Assert.AreEqual("Novi story u grupi \"Krimi klub\".", _kontekst.Notifikacije.First().Poruka);
        }

        [TestMethod]
        public async Task Create_Post_GrupaNePostoji_PorukaSaZamjenskimNazivom()
        {
            _kontekst.ClanstvaGrupe.Add(new ClanstvoGrupe { ClanstvoId = 10, KorisnikId = "k1", GrupaId = 77 });
            await _kontekst.SaveChangesAsync();
            var kontroler = NapraviKontroler("k2");

            await kontroler.Create(new SadrzajGrupe { GrupaId = 77, Naslov = "X", TipSadrzaja = TipSadrzaja.clanak }, null);

            Assert.AreEqual("Nova objava u grupi \"čitalačkoj grupi\".", _kontekst.Notifikacije.Single().Poruka);
        }

        [TestMethod]
        public async Task Create_Post_PdfSaFajlom_LinkVodiNaUploadovaniPdf()
        {
            var kontroler = NapraviKontroler("k2");

            await kontroler.Create(new SadrzajGrupe { GrupaId = 1, Naslov = "Knjiga", TipSadrzaja = TipSadrzaja.pdf }, NapraviPdf(100));

            var link = _kontekst.SadrzajiGrupe.Single(s => s.Naslov == "Knjiga").Link;
            StringAssert.StartsWith(link, "/uploads/");
            StringAssert.EndsWith(link, ".pdf");
            Assert.IsTrue(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", link.TrimStart('/'))));
        }

        // ---------------- Edit (POST) ----------------

        [TestMethod]
        public async Task Edit_IdSeNePoklapa_VracaNotFound()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Edit(1, new SadrzajGrupe { SadrzajId = 2 }, null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_StoryBezSlike_VracaViewSaGreskom()
        {
            var kontroler = NapraviKontroler("k1");
            var izmjena = new SadrzajGrupe { SadrzajId = 2, GrupaId = 1, TipSadrzaja = TipSadrzaja.story, Link = "" };

            var rezultat = await kontroler.Edit(2, izmjena, null) as ViewResult;

            Assert.AreSame(izmjena, rezultat!.Model);
            Assert.IsTrue(kontroler.ModelState.ContainsKey("Link"));
        }

        [TestMethod]
        public async Task Edit_IspravnaIzmjena_SpremaIPreusmjeravaNaGrupu()
        {
            var kontroler = NapraviKontroler("k1");
            var izmjena = new SadrzajGrupe { SadrzajId = 1, AutorId = "k1", GrupaId = 1, Naslov = "Izmijenjena recenzija", TipSadrzaja = TipSadrzaja.clanak };

            var rezultat = await kontroler.Edit(1, izmjena, null) as RedirectToActionResult;

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreEqual("CitalackaGrupa", rezultat!.ControllerName);
            Assert.AreEqual(1, rezultat.RouteValues!["id"]);
            Assert.AreEqual("Izmijenjena recenzija", provjera.SadrzajiGrupe.Single(s => s.SadrzajId == 1).Naslov);
        }

        [TestMethod]
        public async Task Edit_NoviPdf_ZamjenjujeLink()
        {
            var kontroler = NapraviKontroler("k2");
            var izmjena = new SadrzajGrupe { SadrzajId = 3, AutorId = "k2", GrupaId = 1, Naslov = "PDF knjiga", TipSadrzaja = TipSadrzaja.pdf, Link = "/uploads/a.pdf" };

            await kontroler.Edit(3, izmjena, NapraviPdf(50));

            using var provjera = TestHelper.NoviKontekst(_opcije);
            Assert.AreNotEqual("/uploads/a.pdf", provjera.SadrzajiGrupe.Single(s => s.SadrzajId == 3).Link);
        }

        [TestMethod]
        public async Task Edit_ObjavaObrisanaUMedjuvremenu_VracaNotFound()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.Edit(555, new SadrzajGrupe { SadrzajId = 555, GrupaId = 1, TipSadrzaja = TipSadrzaja.clanak }, null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_KonkurentnaIzmjenaPostojeceObjave_BacaIzuzetak()
        {
            using var stubKontekst = new KonkurentnostStubKontekst(_opcije);
            var kontroler = TestHelper.PripremiKontroler(new SadrzajGrupeController(stubKontekst), "k1");

            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
                () => kontroler.Edit(1, new SadrzajGrupe { SadrzajId = 1, GrupaId = 1, TipSadrzaja = TipSadrzaja.clanak }, null));
        }

        // ---------------- Delete / DeleteConfirmed ----------------

        [TestMethod]
        [DataRow(null)]
        [DataRow("k1")]
        [DataRow("admin")]
        public async Task Delete_KorisnikNijeModerator_PreusmjeravaNaAccessDenied(string? korisnikId)
        {
            var kontroler = NapraviKontroler(korisnikId);

            var rezultat = await kontroler.Delete(1) as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow(999)]
        public async Task Delete_ModeratorIdNullIliNepostojeci_VracaNotFound(int? id)
        {
            var kontroler = NapraviKontroler("mod");

            var rezultat = await kontroler.Delete(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_ModeratorPostojecaObjava_VracaViewZaPotvrdu()
        {
            var kontroler = NapraviKontroler("mod");

            var rezultat = await kontroler.Delete(1) as ViewResult;

            Assert.AreEqual("Recenzija", ((SadrzajGrupe)rezultat!.Model!).Naslov);
        }

        [TestMethod]
        public async Task DeleteConfirmed_KorisnikNijeModerator_NistaNeBrise()
        {
            var kontroler = NapraviKontroler("k1");

            var rezultat = await kontroler.DeleteConfirmed(1) as RedirectToActionResult;

            Assert.AreEqual("AccessDenied", rezultat!.ActionName);
            Assert.AreEqual(4, _kontekst.SadrzajiGrupe.Count());
        }

        [TestMethod]
        public async Task DeleteConfirmed_ModeratorNepostojecaObjava_VracaNotFound()
        {
            var kontroler = NapraviKontroler("mod");

            var rezultat = await kontroler.DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task DeleteConfirmed_ModeratorPostojecaObjava_BriseIVracaNaGrupu()
        {
            var kontroler = NapraviKontroler("mod");

            var rezultat = await kontroler.DeleteConfirmed(3) as RedirectToActionResult;

            Assert.AreEqual(1, rezultat!.RouteValues!["id"]);
            Assert.IsFalse(_kontekst.SadrzajiGrupe.Any(s => s.SadrzajId == 3));
        }
    }
}
