using System.Diagnostics;
using DigitalniKlubCitalaca.Controllers;
using DigitalniKlubCitalaca.Models;
using DigitalniKlubCitalaca.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalniKlubCitalaca.Tests.Nejra
{
    /// <summary>
    /// Autorica: Nejra Mašović
    /// Testirane klase: HomeController, ErrorViewModel
    /// </summary>
    [TestClass]
    public class HomeControllerTests
    {
        private HomeController _kontroler = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _kontroler = TestHelper.PripremiKontroler(new HomeController(NullLogger<HomeController>.Instance));
            _kontroler.HttpContext.TraceIdentifier = "trace-0001";
        }

        [TestMethod]
        public void Index_VracaPodrazumijevaniView()
        {
            var rezultat = _kontroler.Index() as ViewResult;

            Assert.IsNotNull(rezultat);
            Assert.IsNull(rezultat.ViewName);
        }

        [TestMethod]
        public void About_VracaView()
        {
            Assert.IsInstanceOfType(_kontroler.About(), typeof(ViewResult));
        }

        [TestMethod]
        public void Help_VracaView()
        {
            Assert.IsInstanceOfType(_kontroler.Help(), typeof(ViewResult));
        }

        [TestMethod]
        public void Privacy_VracaView()
        {
            Assert.IsInstanceOfType(_kontroler.Privacy(), typeof(ViewResult));
        }

        [TestMethod]
        public void AccessDenied_VracaView()
        {
            Assert.IsInstanceOfType(_kontroler.AccessDenied(), typeof(ViewResult));
        }

        [TestMethod]
        public void Error_BezAktivnosti_RequestIdJeTraceIdentifier()
        {
            Activity.Current = null;

            var rezultat = _kontroler.Error() as ViewResult;

            Assert.AreEqual("trace-0001", ((ErrorViewModel)rezultat!.Model!).RequestId);
        }

        [TestMethod]
        public void Error_UnutarAktivnosti_RequestIdJeIdAktivnosti()
        {
            using var aktivnost = new Activity("TestZahtjev").Start();

            var rezultat = _kontroler.Error() as ViewResult;

            Assert.AreEqual(aktivnost.Id, ((ErrorViewModel)rezultat!.Model!).RequestId);
        }

        [TestMethod]
        [DataRow(null, false)]
        [DataRow("", false)]
        [DataRow("trace-0001", true)]
        public void ErrorViewModel_RequestId_ShowRequestIdSamoAkoPostoji(string? requestId, bool ocekivano)
        {
            var model = new ErrorViewModel { RequestId = requestId };

            Assert.AreEqual(ocekivano, model.ShowRequestId);
        }
    }
}
