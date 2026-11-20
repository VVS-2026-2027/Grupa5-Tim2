using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DigitalniKlubCitalaca.Services;

namespace DigitalniKlubCitalaca.Tests
{
    [TestClass]
    public class OcjenaServiceTests
    {
        private OcjenaService _servis;

        [TestInitialize]
        public void Setup() => _servis = new OcjenaService();

        // F1-Z1
        [TestMethod]
        public void Ocijeni_OcjenaIzvanOpsega_BacaIzuzetak()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _servis.Ocijeni(1, 1, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _servis.Ocijeni(1, 1, 6));
        }

        [TestMethod]
        public void Ocijeni_GranicneVrijednosti_NeBacaIzuzetak()
        {
            _servis.Ocijeni(1, 1, 1);
            _servis.Ocijeni(2, 1, 5);
        }


        // F1-Z2
        [TestMethod]
        public void Ocijeni_IstiKorisnikDvaPuta_BrojiSeJednom()
        {
            _servis.Ocijeni(1, 10, 3);
            _servis.Ocijeni(1, 10, 5);
            Assert.AreEqual(1, _servis.BrojOcjena(10));
        }

        [TestMethod]
        public void Ocijeni_RazlicitiKorisnici_BrojeSeSvi()
        {
            _servis.Ocijeni(1, 10, 3);
            _servis.Ocijeni(2, 10, 5);
            _servis.Ocijeni(3, 20, 4);
            Assert.AreEqual(2, _servis.BrojOcjena(10));
        }

        // F1-Z3
        [TestMethod]
        public void ProsjecnaOcjena_ViseOcjena_ZaokruzujeNaJednuDecimalu()
        {
            _servis.Ocijeni(1, 10, 5);
            _servis.Ocijeni(2, 10, 4);
            _servis.Ocijeni(3, 10, 4);
            Assert.AreEqual(4.3, _servis.ProsjecnaOcjena(10), 0.0001);
        }

        [TestMethod]
        public void ProsjecnaOcjena_PromijenjenaOcjena_RacunaNovuVrijednost()
        {
            _servis.Ocijeni(1, 10, 2);
            _servis.Ocijeni(1, 10, 5);
            _servis.Ocijeni(2, 10, 4);
            Assert.AreEqual(4.5, _servis.ProsjecnaOcjena(10), 0.0001);
        }


        // F1-Z4
        [TestMethod]
        public void ProsjecnaOcjena_KnjigaBezOcjena_VracaNulu()
        {
            _servis.Ocijeni(1, 10, 5);
            Assert.AreEqual(0, _servis.ProsjecnaOcjena(99), 0.0001);
        }

    }
}