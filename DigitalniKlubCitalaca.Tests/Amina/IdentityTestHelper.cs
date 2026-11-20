using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DigitalniKlubCitalaca.Tests.Amina
{
    /// <summary>
    /// Pomocne metode za testiranje Identity Razor stranica (Register, Login, Logout).
    /// Autorica: Amina Peštalić
    /// </summary>
    public static class IdentityTestHelper
    {
        public const string LinkPotvrde = "https://dkc.test/Identity/Account/ConfirmEmail?code=abc";

        /// <summary>
        /// Zamjenski objekat za SignInManager. Konstruktor zahtijeva UserManager, IHttpContextAccessor i
        /// IUserClaimsPrincipalFactory, a ostalo moze biti null.
        /// </summary>
        public static Mock<SignInManager<Korisnik>> SignInManagerMock(UserManager<Korisnik> userManager)
        {
            var mock = new Mock<SignInManager<Korisnik>>(
                userManager,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<Korisnik>>(),
                null!, null!, null!, null!);

            mock.Setup(s => s.GetExternalAuthenticationSchemesAsync())
                .ReturnsAsync(new List<AuthenticationScheme>());

            return mock;
        }

        /// <summary>
        /// Postavlja PageContext (HttpContext, ModelState, RouteData), zamjenski IAuthenticationService i IUrlHelper
        /// tako da se PageModel moze izvrsiti van web servera.
        /// </summary>
        public static Mock<IAuthenticationService> PripremiStranicu(PageModel stranica)
        {
            var authMock = new Mock<IAuthenticationService>();
            var servisi = new ServiceCollection().AddSingleton(authMock.Object).BuildServiceProvider();
            var httpContext = new DefaultHttpContext { RequestServices = servisi };

            var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
            stranica.PageContext = new PageContext(actionContext);

            var url = new Mock<IUrlHelper>();
            url.Setup(u => u.ActionContext).Returns(actionContext);
            url.Setup(u => u.Content(It.IsAny<string>())).Returns<string>(putanja => putanja.TrimStart('~'));
            url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns(LinkPotvrde);
            stranica.Url = url.Object;

            return authMock;
        }

        public static Mock<UserManager<Korisnik>> UserManagerMock()
        {
            return TestHelper.UserManagerMock(null);
        }
    }
}
