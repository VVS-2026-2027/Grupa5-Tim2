using System.Security.Claims;
using DigitalniKlubCitalaca.Data;
using DigitalniKlubCitalaca.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Helpers
{
    /// <summary>
    /// Zajednicke pomocne metode za sve clanice tima.
    /// Baza je zamijenjena EF Core InMemory bazom (nije aktivna prava baza).
    /// </summary>
    public static class TestHelper
    {
        /// <summary>Opcije za novu, izolovanu InMemory bazu (svaki test dobija svoju).</summary>
        public static DbContextOptions<ApplicationDbContext> NoveOpcije(string? imeBaze = null)
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(imeBaze ?? Guid.NewGuid().ToString(), b => b.EnableNullChecks(false))
                .Options;
        }

        public static ApplicationDbContext NoviKontekst(DbContextOptions<ApplicationDbContext> opcije)
        {
            return new ApplicationDbContext(opcije);
        }

        public static Korisnik NoviKorisnik(string id, Uloga uloga = Uloga.korisnik)
        {
            return new Korisnik
            {
                Id = id,
                UserName = id + "@test.ba",
                Email = id + "@test.ba",
                // fiksan ConcurrencyStamp: nova instanca istog korisnika prolazi provjeru konkurentnosti pri Update()
                ConcurrencyStamp = "stamp-" + id,
                Ime = "Ime" + id,
                Prezime = "Prezime" + id,
                ProfilnaSlika = string.Empty,
                Uloga = uloga,
                StatusNaloga = StatusNaloga.aktivan
            };
        }

        /// <summary>
        /// Zamjenski objekat (mock) za UserManager - GetUserAsync vraca zadanog korisnika (ili null = neprijavljen).
        /// </summary>
        public static Mock<UserManager<Korisnik>> UserManagerMock(Korisnik? prijavljeniKorisnik)
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var mock = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(prijavljeniKorisnik);

            return mock;
        }

        /// <summary>
        /// Postavlja HttpContext (sa korisnikom), TempData i zamjenski IUrlHelper na kontroler,
        /// kako bi Url.Action(...), TempData i User radili van web servera.
        /// </summary>
        public static T PripremiKontroler<T>(T kontroler, string? korisnikId = null) where T : Controller
        {
            var claims = korisnikId == null
                ? new List<Claim>()
                : new List<Claim> { new Claim(ClaimTypes.NameIdentifier, korisnikId) };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, korisnikId == null ? null : "Test"))
            };

            kontroler.ControllerContext = new ControllerContext { HttpContext = httpContext };
            kontroler.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            var url = new Mock<IUrlHelper>();
            url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/test-link");
            kontroler.Url = url.Object;

            return kontroler;
        }

        /// <summary>Putanja do fajla u TestData folderu (kopira se u bin pri build-u).</summary>
        public static string PutanjaPodataka(params string[] dijelovi)
        {
            return Path.Combine(new[] { AppContext.BaseDirectory, "TestData" }.Concat(dijelovi).ToArray());
        }
    }
}
