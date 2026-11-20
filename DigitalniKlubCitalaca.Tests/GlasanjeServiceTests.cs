using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DigitalniKlubCitalaca.Services;

namespace DigitalniKlubCitalaca.Tests
{
    [TestClass]
    public class GlasanjeServiceTests
    {
        private readonly DateTime _rok = new DateTime(2026, 11, 30);
        private readonly DateTime _prijeRoka = new DateTime(2026, 11, 20);
        private GlasanjeService _servis;

        [TestInitialize]
        public void Setup() => _servis = new GlasanjeService(new[] { 1, 2, 3, 4 }, _rok);

        // F2-Z1
        [TestMethod]
        public void Glasaj_NijeClan_BacaIzuzetak()
        {
            Assert.ThrowsException<InvalidOperationException>(() => _servis.Glasaj(99, 10, _prijeRoka));
        }

        [TestMethod]
        public void Glasaj_Clan_MozeGlasati()
        {
            _servis.Glasaj(1, 10, _prijeRoka);
        }
    }
}