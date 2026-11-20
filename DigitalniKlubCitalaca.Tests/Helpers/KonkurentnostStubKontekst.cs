using DigitalniKlubCitalaca.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalniKlubCitalaca.Tests.Helpers
{
    /// <summary>
    /// Rucno napisan zamjenski objekat (stub) baze: SaveChangesAsync uvijek baca
    /// DbUpdateConcurrencyException. Sluzi da se simulira istovremena izmjena istog zapisa
    /// od strane dva korisnika, sto se sa InMemory bazom ne moze izazvati prirodno.
    /// Mogu ga koristiti sve clanice za "throw;" granu u Edit akcijama.
    /// </summary>
    public class KonkurentnostStubKontekst : ApplicationDbContext
    {
        public KonkurentnostStubKontekst(DbContextOptions<ApplicationDbContext> opcije) : base(opcije)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateConcurrencyException("Simulirana konkurentna izmjena.");
        }
    }
}
