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
        // F2-Z2
        [TestMethod]
        public void Glasaj_ClanGlasaDrugiPut_BacaIzuzetak()
        {
            _servis.Glasaj(1, 10, _prijeRoka);
            Assert.ThrowsException<InvalidOperationException>(() => _servis.Glasaj(1, 20, _prijeRoka));
        }

        [TestMethod]
        public void BrojGlasova_VracaBrojGlasovaZaKnjigu()
        {
            _servis.Glasaj(1, 10, _prijeRoka);
            _servis.Glasaj(2, 10, _prijeRoka);
            _servis.Glasaj(3, 20, _prijeRoka);
            Assert.AreEqual(2, _servis.BrojGlasova(10));
        }


        // F2-Z3
        [TestMethod]
        public void Glasaj_NakonRoka_BacaIzuzetakIGlasSeNeBroji()
        {
            Assert.ThrowsException<InvalidOperationException>(() => _servis.Glasaj(1, 10, _rok.AddMinutes(1)));
            Assert.AreEqual(0, _servis.BrojGlasova(10));
        }

        [TestMethod]
        public void Glasaj_TacnoURoku_JeDozvoljeno()
        {
            _servis.Glasaj(1, 10, _rok);
            Assert.AreEqual(1, _servis.BrojGlasova(10));
        }

        // F2-Z4
        [TestMethod]
        public void Pobjednik_KnjigaSNajviseGlasova()
        {
            _servis.Glasaj(1, 10, _prijeRoka);
            _servis.Glasaj(2, 20, _prijeRoka);
            _servis.Glasaj(3, 20, _prijeRoka);
            Assert.AreEqual(20, _servis.Pobjednik());
        }

        [TestMethod]
        public void Pobjednik_BezGlasova_VracaNull()
        {
            Assert.IsNull(_servis.Pobjednik());
        }
    }
}