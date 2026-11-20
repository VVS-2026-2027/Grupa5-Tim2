using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalniKlubCitalaca.Services
{
    public class OcjenaService
    {
        public void Ocijeni(int korisnikId, int knjigaId, int ocjena)
        {
            if (ocjena < 1 || ocjena > 5)
                throw new ArgumentOutOfRangeException(nameof(ocjena), "Ocjena mora biti između 1 i 5.");
        }
    }
}