using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalniKlubCitalaca.Services
{
    public class OcjenaService
    {
        private const int MinOcjena = 1;
        private const int MaxOcjena = 5;

        private readonly Dictionary<(int KorisnikId, int KnjigaId), int> _ocjene = new Dictionary<(int, int), int>();

        public void Ocijeni(int korisnikId, int knjigaId, int ocjena)
        {
            if (ocjena < MinOcjena || ocjena > MaxOcjena)
                throw new ArgumentOutOfRangeException(nameof(ocjena), $"Ocjena mora biti između {MinOcjena} i {MaxOcjena}.");

            _ocjene[(korisnikId, knjigaId)] = ocjena;
        }

        public int BrojOcjena(int knjigaId) => OcjeneKnjige(knjigaId).Count();

        public double ProsjecnaOcjena(int knjigaId)
        {
            var ocjene = OcjeneKnjige(knjigaId).ToList();
            if (ocjene.Count == 0) return 0;
            return Math.Round(ocjene.Average(), 1);
        }

        private IEnumerable<int> OcjeneKnjige(int knjigaId) =>
            _ocjene.Where(o => o.Key.KnjigaId == knjigaId).Select(o => o.Value);
    }
}