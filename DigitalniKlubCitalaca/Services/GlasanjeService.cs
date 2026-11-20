using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalniKlubCitalaca.Services
{
    public class GlasanjeService
    {
        private readonly HashSet<int> _clanovi;
        private readonly DateTime _rok;

        public GlasanjeService(IEnumerable<int> clanoviKluba, DateTime rokGlasanja)
        {
            _clanovi = new HashSet<int>(clanoviKluba);
            _rok = rokGlasanja;
        }

        public void Glasaj(int clanId, int knjigaId, DateTime vrijeme)
        {
            if (!_clanovi.Contains(clanId))
                throw new InvalidOperationException("Samo članovi kluba mogu glasati.");
        }
        public int BrojGlasova(int knjigaId)
        {
            throw new NotImplementedException();
        }

    }
}