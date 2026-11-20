namespace DigitalniKlubCitalaca.Services
{
    /// <summary>
    /// Modul za slanje e-mail obavijesti korisnicima.
    /// NIJE JOS IMPLEMENTIRAN - u unit testovima se koristi zamjenski objekat (mock).
    /// </summary>
    public interface IEmailService
    {
        Task PosaljiObavijestAsync(string korisnikId, string naslov, string poruka);
    }
}
