namespace DigitalniKlubCitalaca.Services
{
    /// <summary>
    /// Modul za spremanje PDF knjiga u vanjsko skladiste (npr. cloud storage umjesto wwwroot foldera).
    /// NIJE JOS IMPLEMENTIRAN - u unit testovima se koristi zamjenski objekat (mock).
    /// Dok se ne implementira, KnjigaController sprema PDF lokalno u wwwroot/biblioteka.
    /// </summary>
    public interface IPdfStorageService
    {
        /// <returns>Javna putanja (URL) do spremljenog PDF-a.</returns>
        Task<string> SpremiAsync(IFormFile pdfFile);
    }
}
