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
    }
}