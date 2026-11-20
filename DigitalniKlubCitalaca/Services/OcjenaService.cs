using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalniKlubCitalaca.Services
{
    public class OcjenaService
    {
        private readonly Dictionary<(int KorisnikId, int KnjigaId), int> _ocjene = new Dictionary<(int, int), int>();

        public void Ocijeni(int korisnikId, int knjigaId, int ocjena)
        {
            if (ocjena < 1 || ocjena > 5)
                throw new ArgumentOutOfRangeException(nameof(ocjena), "Ocjena mora biti između 1 i 5.");

            _ocjene[(korisnikId, knjigaId)] = ocjena;
        }

        public int BrojOcjena(int knjigaId) => _ocjene.Keys.Count(k => k.KnjigaId == knjigaId);


        public double ProsjecnaOcjena(int knjigaId)
        {
            var ocjene = _ocjene.Where(o => o.Key.KnjigaId == knjigaId).Select(o => o.Value).ToList();
            if (ocjene.Count == 0) return 0;
            return Math.Round(ocjene.Average(), 1);
        }
    }
}