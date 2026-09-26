namespace TPARCIAL.Services;

/// <summary>
/// Configuración de Algolia. La AdminApiKey NO se guarda en appsettings.json:
/// se lee de user-secrets (desarrollo) o de la variable de entorno Algolia__AdminApiKey.
/// </summary>
public class AlgoliaOptions
{
    public const string Seccion = "Algolia";

    public string ApplicationId { get; set; } = string.Empty;
    public string AdminApiKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = "incidencias";

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(ApplicationId) && !string.IsNullOrWhiteSpace(AdminApiKey);
}
