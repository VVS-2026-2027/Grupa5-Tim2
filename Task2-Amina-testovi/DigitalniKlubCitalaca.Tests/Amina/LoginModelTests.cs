using System.Xml.Linq;
using DigitalniKlubCitalaca.Areas.Identity.Pages.Account;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace DigitalniKlubCitalaca.Tests.Amina
{
    /// <summary>
    /// Autorica: Amina Peštalić
    /// Testirana klasa: LoginModel (Areas/Identity/Pages/Account/Login.cshtml.cs)
    /// Zamjenski objekti: UserManager, SignInManager, IAuthenticationService (mock)
    /// </summary>
    [TestClass]
    public class LoginModelTests
    {
        private Mock<UserManager<Korisnik>> _userManager = null!;
        private Mock<SignInManager<Korisnik>> _signInManager = null!;
        private Mock<IAuthenticationService> _auth = null!;
        private LoginModel _stranica = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            var amina = TestHelper.NoviKorisnik("amina");
            amina.UserName = "amina.p";

            _userManager = IdentityTestHelper.UserManagerMock();
            _userManager.Setup(u => u.Users).Returns(new List<Korisnik> { amina, TestHelper.NoviKorisnik("nejra") }.AsQueryable());

            _signInManager = IdentityTestHelper.SignInManagerMock(_userManager.Object);

            _stranica = new LoginModel(_signInManager.Object, NullLogger<LoginModel>.Instance, _userManager.Object)
            {
                Input = new LoginModel.InputModel { Email = "amina@test.ba", Password = "Lozinka1!", RememberMe = true }
            };
            _auth = IdentityTestHelper.PripremiStranicu(_stranica);
        }

        // ---------------- Data driven izvor (XML) ----------------

        // Ishod iz XML-a -> odgovor koji vraca zamjenski SignInManager
        private static readonly Dictionary<string, IdentitySignInResult> SignInOdgovori = new()
        {
            ["uspjeh"] = IdentitySignInResult.Success,
            ["zakljucan"] = IdentitySignInResult.LockedOut,
            ["dvofaktorska"] = IdentitySignInResult.TwoFactorRequired,
            ["neuspjeh"] = IdentitySignInResult.Failed
        };

        public static IEnumerable<object[]> IshodiIzXml()
        {
            return XDocument.Load(TestHelper.PutanjaPodataka("Amina", "prijava_ishodi.xml"))
                .Descendants("prijava")
                .Select(p => new object[]
                {
                    (string)p.Attribute("ishod")!,
                    (string)p.Attribute("ocekivaniRezultat")!,
                    (string)p.Attribute("ocekivanaStranica")!
                });
        }

        // ---------------- OnGetAsync ----------------

        [TestMethod]
        public async Task OnGetAsync_BezReturnUrl_PostavljaPocetnuStranicuIOdjavljujeVanjskuShemu()
        {
            await _stranica.OnGetAsync();

            Assert.AreEqual("/", _stranica.ReturnUrl);
            Assert.AreEqual(0, _stranica.ExternalLogins.Count);
            _auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), IdentityConstants.ExternalScheme, It.IsAny<AuthenticationProperties>()), Times.Once);
        }

        [TestMethod]
        public async Task OnGetAsync_PostojiPorukaOGresci_PorukaPrikazanaUModelState()
        {
            _stranica.ErrorMessage = "Sesija je istekla.";

            await _stranica.OnGetAsync("/Knjiga");

            Assert.AreEqual("/Knjiga", _stranica.ReturnUrl);
            Assert.AreEqual("Sesija je istekla.", _stranica.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
        }

        // ---------------- OnPostAsync ----------------

        [TestMethod]
        [DynamicData(nameof(IshodiIzXml), DynamicDataSourceType.Method)]
        public async Task OnPostAsync_IshodPrijaveIzXml_OcekivaniRezultat(string ishod, string ocekivaniTip, string ocekivanaStranica)
        {
            _signInManager.Setup(s => s.PasswordSignInAsync("amina.p", "Lozinka1!", true, false)).ReturnsAsync(SignInOdgovori[ishod]);

            var rezultat = await _stranica.OnPostAsync();

            Assert.AreEqual(ocekivaniTip, rezultat.GetType().Name, ishod);
            Assert.AreEqual(ocekivanaStranica, (rezultat as RedirectToPageResult)?.PageName ?? string.Empty, ishod);
        }

        [TestMethod]
        public async Task OnPostAsync_UspjesnaPrijavaSaReturnUrl_VracaNaTrazenuStranicu()
        {
            _signInManager.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false))
                          .ReturnsAsync(IdentitySignInResult.Success);

            var rezultat = await _stranica.OnPostAsync("/CitalackaGrupa/DostupneGrupe") as LocalRedirectResult;

            Assert.AreEqual("/CitalackaGrupa/DostupneGrupe", rezultat!.Url);
        }

        [TestMethod]
        public async Task OnPostAsync_PrijavaPoEmailu_SignInDobijaKorisnickoImeANeEmail()
        {
            _signInManager.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                          .ReturnsAsync(IdentitySignInResult.Success);

            await _stranica.OnPostAsync();

            _signInManager.Verify(s => s.PasswordSignInAsync("amina.p", "Lozinka1!", true, false), Times.Once);
        }

        [TestMethod]
        public async Task OnPostAsync_NepostojeciEmail_PorukaNaBosanskomBezPokusajaPrijave()
        {
            _stranica.Input.Email = "nema.me@test.ba";

            var rezultat = await _stranica.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            Assert.AreEqual("Ne postoji korisnik sa unesenim emailom.", _stranica.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
            _signInManager.Verify(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_PogresnaLozinka_PorukaInvalidLoginAttempt()
        {
            _signInManager.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false))
                          .ReturnsAsync(IdentitySignInResult.Failed);

            await _stranica.OnPostAsync();

            Assert.AreEqual("Invalid login attempt.", _stranica.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
        }

        [TestMethod]
        public async Task OnPostAsync_NevalidanUnos_VracaStranicuBezTrazenjaKorisnika()
        {
            _stranica.ModelState.AddModelError("Input.Email", "The Email field is required.");

            var rezultat = await _stranica.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            _userManager.Verify(u => u.Users, Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_DvofaktorskaPrijava_ProsljedjujeRememberMe()
        {
            _signInManager.Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), false))
                          .ReturnsAsync(IdentitySignInResult.TwoFactorRequired);

            var rezultat = await _stranica.OnPostAsync("/Profil") as RedirectToPageResult;

            Assert.AreEqual("/Profil", rezultat!.RouteValues!["ReturnUrl"]);
            Assert.AreEqual(true, rezultat.RouteValues["RememberMe"]);
        }
    }
}
