namespace TPARCIAL.Services;

/// <summary>
/// Configuración de PieHost (PieSocket). Se lee de la sección "PieHost" (user-secrets en desarrollo,
/// variables de entorno PieHost__* en Render). El ApiSecret solo se usa en el servidor y nunca llega al navegador.
/// </summary>
public class PieHostOptions
{
    public const string Seccion = "PieHost";

    public string ClusterId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Canal por defecto si no se define WebSocketUrl.</summary>
    public string Canal { get; set; } = "incidencias";

    /// <summary>URL del canal WebSocket ya configurado, p. ej. wss://CLUSTER.piesocket.com/v3/CANAL?api_key=KEY.</summary>
    public string WebSocketUrl { get; set; } = string.Empty;

    /// <summary>URL a la que se conecta el navegador (solo contiene la ApiKey pública).</summary>
    public string? UrlCliente =>
        !string.IsNullOrWhiteSpace(WebSocketUrl) ? WebSocketUrl
        : !string.IsNullOrWhiteSpace(ClusterId) && !string.IsNullOrWhiteSpace(ApiKey)
            ? $"wss://{ClusterId}.piesocket.com/v3/{Uri.EscapeDataString(Canal)}?api_key={Uri.EscapeDataString(ApiKey)}"
            : null;

    /// <summary>Canal donde publica el servidor: el mismo de WebSocketUrl (último segmento de la ruta) o Canal.</summary>
    public string CanalEfectivo =>
        Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out var uri) && uri.Segments.Length > 0
            ? Uri.UnescapeDataString(uri.Segments[^1].Trim('/'))
            : Canal;

    public bool ClienteHabilitado => UrlCliente is not null;

    /// <summary>El servidor puede publicar (requiere ClusterId, ApiKey y ApiSecret).</summary>
    public bool PublicacionHabilitada =>
        !string.IsNullOrWhiteSpace(ClusterId) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}
