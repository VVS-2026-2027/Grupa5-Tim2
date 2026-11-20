namespace DigitalniKlubCitalaca.Services
{
    /// <summary>
    /// Modul za bilježenje administratorskih akcija (dnevnik aktivnosti / audit log).
    /// NIJE JOS IMPLEMENTIRAN - u unit testovima se koristi zamjenski objekat (mock).
    /// </summary>
    public interface IDnevnikAktivnostiService
    {
        Task ZabiljeziAsync(string adminId, string akcija, string korisnikId);
    }
}
