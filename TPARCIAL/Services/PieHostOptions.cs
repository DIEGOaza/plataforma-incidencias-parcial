namespace TPARCIAL.Services;

/// <summary>
/// Configuración de PieHost (PieSocket). ClusterId, ApiKey y Canal pueden ir en appsettings.json;
/// el ApiSecret SOLO en user-secrets o en la variable de entorno PieHost__ApiSecret (nunca llega al navegador).
/// </summary>
public class PieHostOptions
{
    public const string Seccion = "PieHost";

    public string ClusterId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Canal { get; set; } = "incidencias";

    /// <summary>El navegador puede suscribirse (solo requiere ClusterId y la ApiKey pública).</summary>
    public bool ClienteHabilitado => !string.IsNullOrWhiteSpace(ClusterId) && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>El servidor puede publicar (requiere además el ApiSecret).</summary>
    public bool PublicacionHabilitada => ClienteHabilitado && !string.IsNullOrWhiteSpace(ApiSecret);
}
