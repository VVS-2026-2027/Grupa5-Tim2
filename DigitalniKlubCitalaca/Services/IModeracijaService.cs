namespace DigitalniKlubCitalaca.Services
{
    /// <summary>
    /// Modul za automatsku moderaciju komentara (provjera zabranjenih rijeci, uvreda, spama).
    /// NIJE JOS IMPLEMENTIRAN - u unit testovima se koristi zamjenski objekat (mock).
    /// </summary>
    public interface IModeracijaService
    {
        Task<bool> JeNeprikladanAsync(string tekst);
    }
}
