using DigitalniKlubCitalaca.Areas.Identity.Pages.Account;
using DigitalniKlubCitalaca.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Amina
{
    /// <summary>
    /// Autorica: Amina Peštalić
    /// Testirana klasa: LogoutModel (Areas/Identity/Pages/Account/Logout.cshtml.cs)
    /// </summary>
    [TestClass]
    public class LogoutModelTests
    {
        private Mock<SignInManager<Korisnik>> _signInManager = null!;
        private LogoutModel _stranica = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _signInManager = IdentityTestHelper.SignInManagerMock(IdentityTestHelper.UserManagerMock().Object);
            _stranica = new LogoutModel(_signInManager.Object, NullLogger<LogoutModel>.Instance);
            IdentityTestHelper.PripremiStranicu(_stranica);
        }

        [TestMethod]
        public async Task OnPost_SaReturnUrl_OdjavljujeIVracaNaTuStranicu()
        {
            var rezultat = await _stranica.OnPost("/Knjiga") as LocalRedirectResult;

            Assert.AreEqual("/Knjiga", rezultat!.Url);
            _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
        }

        [TestMethod]
        public async Task OnPost_BezReturnUrl_OdjavljujeIOsvjezavaIstuStranicu()
        {
            var rezultat = await _stranica.OnPost() as RedirectToPageResult;

            Assert.IsNotNull(rezultat);
            Assert.IsNull(rezultat.PageName);
            _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
        }

        [TestMethod]
        public async Task OnPost_OdjavaNeUspije_IzuzetakSePropagira()
        {
            _signInManager.Setup(s => s.SignOutAsync()).ThrowsAsync(new InvalidOperationException("Kolačić se ne može obrisati"));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _stranica.OnPost("/Knjiga"));
        }
    }
}
