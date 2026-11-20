using System.ComponentModel.DataAnnotations;
using DigitalniKlubCitalaca.Areas.Identity.Pages.Account;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Amina
{
    /// <summary>
    /// Autorica: Amina Peštalić
    /// Testirana klasa: RegisterModel (Areas/Identity/Pages/Account/Register.cshtml.cs)
    /// Zamjenski objekat: IEmailSender - slanje e-maila za potvrdu naloga nije implementirano
    /// (aplikacija koristi podrazumijevani NoOp sender koji nista ne salje).
    /// </summary>
    [TestClass]
    public class RegisterModelTests
    {
        private Mock<UserManager<Korisnik>> _userManager = null!;
        private Mock<IUserStore<Korisnik>> _userStore = null!;
        private Mock<IUserEmailStore<Korisnik>> _emailStore = null!;
        private Mock<SignInManager<Korisnik>> _signInManager = null!;
        private Mock<IEmailSender> _emailSender = null!;
        private Korisnik? _kreiraniKorisnik;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _kreiraniKorisnik = null;

            _userManager = IdentityTestHelper.UserManagerMock();
            _userManager.Setup(u => u.SupportsUserEmail).Returns(true);
            _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                        .Callback<Korisnik, string>((k, _) => _kreiraniKorisnik = k)
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(u => u.GetUserIdAsync(It.IsAny<Korisnik>())).ReturnsAsync("novi-id");
            _userManager.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<Korisnik>())).ReturnsAsync("token-123");

            _userStore = new Mock<IUserStore<Korisnik>>();
            _emailStore = _userStore.As<IUserEmailStore<Korisnik>>();

            _signInManager = IdentityTestHelper.SignInManagerMock(_userManager.Object);
            _emailSender = new Mock<IEmailSender>();
        }

        private RegisterModel NapraviStranicu()
        {
            var stranica = new RegisterModel(_userManager.Object, _userStore.Object, _signInManager.Object,
                NullLogger<RegisterModel>.Instance, _emailSender.Object)
            {
                Input = new RegisterModel.InputModel
                {
                    Email = "amina@test.ba", Ime = "Amina", Prezime = "Peštalić", UserName = "amina",
                    Grad = "Sarajevo", Regija = "KS", Password = "Lozinka1!", ConfirmPassword = "Lozinka1!"
                }
            };
            IdentityTestHelper.PripremiStranicu(stranica);
            return stranica;
        }

        // ---------------- Data driven izvor (CSV) ----------------

        public static IEnumerable<object[]> UnosiIzCsv()
        {
            return File.ReadAllLines(TestHelper.PutanjaPodataka("Amina", "registracija_validacija.csv"))
                .Skip(1)
                .Select(red => red.Split(';'))
                .Select(p => new object[] { p[0], p[1], p[2], p[3], p[4], p[5], bool.Parse(p[6]) });
        }

        // ---------------- Validacija forme (InputModel) ----------------

        [TestMethod]
        [DynamicData(nameof(UnosiIzCsv), DynamicDataSourceType.Method)]
        public void InputModel_PodaciIzCsv_ValidacijaKaoOcekivano(string email, string ime, string prezime, string korisnickoIme,
            string lozinka, string potvrda, bool ocekivanoValidno)
        {
            var unos = new RegisterModel.InputModel
            {
                Email = email, Ime = ime, Prezime = prezime, UserName = korisnickoIme, Password = lozinka, ConfirmPassword = potvrda
            };
            var greske = new List<ValidationResult>();

            var validno = Validator.TryValidateObject(unos, new ValidationContext(unos), greske, validateAllProperties: true);

            Assert.AreEqual(ocekivanoValidno, validno, string.Join(" | ", greske.Select(g => g.ErrorMessage)));
        }

        [TestMethod]
        public void InputModel_BezGradaIRegije_IpakValidno()
        {
            var unos = new RegisterModel.InputModel
            {
                Email = "a@b.ba", Ime = "A", Prezime = "B", UserName = "ab", Password = "123456", ConfirmPassword = "123456"
            };

            var validno = Validator.TryValidateObject(unos, new ValidationContext(unos), null, validateAllProperties: true);

            Assert.IsTrue(validno);
        }

        // ---------------- Konstruktor / OnGetAsync ----------------

        [TestMethod]
        public void Konstruktor_StoreBezPodrskeZaEmail_BacaNotSupportedException()
        {
            _userManager.Setup(u => u.SupportsUserEmail).Returns(false);

            Assert.ThrowsException<NotSupportedException>(() => NapraviStranicu());
        }

        [TestMethod]
        public async Task OnGetAsync_SaReturnUrl_PamtiUrlIUcitavaVanjskePrijave()
        {
            var stranica = NapraviStranicu();

            await stranica.OnGetAsync("/CitalackaGrupa");

            Assert.AreEqual("/CitalackaGrupa", stranica.ReturnUrl);
            Assert.IsNotNull(stranica.ExternalLogins);
        }

        // ---------------- OnPostAsync ----------------

        [TestMethod]
        public async Task OnPostAsync_IspravniPodaci_KorisnikKreiranSaSvimPoljima()
        {
            var stranica = NapraviStranicu();

            await stranica.OnPostAsync();

            Assert.IsNotNull(_kreiraniKorisnik);
            Assert.AreEqual("Amina", _kreiraniKorisnik.Ime);
            Assert.AreEqual("Peštalić", _kreiraniKorisnik.Prezime);
            Assert.AreEqual("Sarajevo", _kreiraniKorisnik.Grad);
            Assert.AreEqual("KS", _kreiraniKorisnik.Regija);
            _emailStore.Verify(e => e.SetEmailAsync(_kreiraniKorisnik, "amina@test.ba", It.IsAny<CancellationToken>()), Times.Once);
            _userStore.Verify(s => s.SetUserNameAsync(_kreiraniKorisnik, "amina", It.IsAny<CancellationToken>()), Times.Once);
            _userManager.Verify(u => u.CreateAsync(_kreiraniKorisnik, "Lozinka1!"), Times.Once);
        }

        [TestMethod]
        public async Task OnPostAsync_BezPotvrdeNaloga_PrijavljujeIVodiNaDostupneGrupe()
        {
            var stranica = NapraviStranicu();

            var rezultat = await stranica.OnPostAsync() as RedirectToActionResult;

            Assert.AreEqual("DostupneGrupe", rezultat!.ActionName);
            Assert.AreEqual("CitalackaGrupa", rezultat.ControllerName);
            _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), false, null), Times.Once);
        }

        [TestMethod]
        public async Task OnPostAsync_PotrebnaPotvrdaNaloga_VodiNaRegisterConfirmationBezPrijave()
        {
            _userManager.Object.Options.SignIn.RequireConfirmedAccount = true;
            var stranica = NapraviStranicu();

            var rezultat = await stranica.OnPostAsync("/Profil") as RedirectToPageResult;

            Assert.AreEqual("RegisterConfirmation", rezultat!.PageName);
            Assert.AreEqual("amina@test.ba", rezultat.RouteValues!["email"]);
            Assert.AreEqual("/Profil", rezultat.RouteValues["returnUrl"]);
            _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_IdentityOdbijeLozinku_GreskeSePrikazujuNaFormi()
        {
            _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Failed(
                            new IdentityError { Description = "Lozinka mora imati bar jedno veliko slovo." },
                            new IdentityError { Description = "Korisničko ime je zauzeto." }));
            var stranica = NapraviStranicu();

            var rezultat = await stranica.OnPostAsync();

            var poruke = stranica.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).ToList();
            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            CollectionAssert.AreEqual(new List<string> { "Lozinka mora imati bar jedno veliko slovo.", "Korisničko ime je zauzeto." }, poruke);
        }

        [TestMethod]
        public async Task OnPostAsync_NevalidnaForma_KorisnikSeNeKreira()
        {
            var stranica = NapraviStranicu();
            stranica.ModelState.AddModelError("Input.Email", "The Email field is not a valid e-mail address.");

            var rezultat = await stranica.OnPostAsync();

            Assert.IsInstanceOfType(rezultat, typeof(PageResult));
            _userManager.Verify(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        }

        // ---------------- Zamjenski objekat: IEmailSender (mock) ----------------

        [TestMethod]
        public async Task OnPostAsync_UspjesnaRegistracija_SaljeSeJedanMailSaLinkomZaPotvrdu()
        {
            var stranica = NapraviStranicu();

            await stranica.OnPostAsync();

            _emailSender.Verify(e => e.SendEmailAsync("amina@test.ba", "Confirm your email",
                It.Is<string>(poruka => poruka.Contains("dkc.test/Identity/Account/ConfirmEmail"))), Times.Once);
        }

        [TestMethod]
        public async Task OnPostAsync_NeuspjesnaRegistracija_MailSeNeSalje()
        {
            _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email je zauzet." }));
            var stranica = NapraviStranicu();

            await stranica.OnPostAsync();

            _emailSender.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAsync_EmailServisNedostupan_IzuzetakIKorisnikNijePrijavljen()
        {
            _emailSender.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                        .ThrowsAsync(new InvalidOperationException("SMTP server nedostupan"));
            var stranica = NapraviStranicu();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => stranica.OnPostAsync());

            _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        }
    }
}
