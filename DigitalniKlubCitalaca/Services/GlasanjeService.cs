using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalniKlubCitalaca.Services
{
    public class GlasanjeService
    {
        private readonly HashSet<int> _clanovi;
        private readonly DateTime _rok;
        private readonly List<(int ClanId, int KnjigaId)> _glasovi = new List<(int, int)>();

        public GlasanjeService(IEnumerable<int> clanoviKluba, DateTime rokGlasanja)
        {
            _clanovi = new HashSet<int>(clanoviKluba);
            _rok = rokGlasanja;
        }

        public void Glasaj(int clanId, int knjigaId, DateTime vrijeme)
        {
            if (!_clanovi.Contains(clanId))
                throw new InvalidOperationException("Samo članovi kluba mogu glasati.");

            if (_glasovi.Any(g => g.ClanId == clanId))
                throw new InvalidOperationException("Član je već glasao.");

            _glasovi.Add((clanId, knjigaId));
        }

        public int BrojGlasova(int knjigaId) => _glasovi.Count(g => g.KnjigaId == knjigaId);
    }
}