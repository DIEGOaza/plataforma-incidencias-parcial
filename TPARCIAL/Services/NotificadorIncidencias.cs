using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TPARCIAL.Hubs;

namespace TPARCIAL.Services;

/// <summary>Evento en tiempo real sobre una incidencia.</summary>
/// <param name="Tipo">"nueva", "abierta" (su estado pasó a Abierta) o "actualizada".</param>
public record IncidenciaNotificacion(
    Guid EventoId,
    string Tipo,
    int Id,
    string Descripcion,
    string Estado,
    string? Estacion,
    DateTime FechaReporte);

public interface INotificadorIncidencias
{
    Task NotificarAsync(IncidenciaNotificacion notificacion, CancellationToken ct = default);
}

/// <summary>
/// Envía las notificaciones a los clientes conectados por SignalR y, si está configurado, también al canal de PieHost.
/// Un fallo al notificar se registra, pero nunca interrumpe la operación que lo originó.
/// </summary>
public class NotificadorIncidencias(
    IHubContext<IncidenciasHub> hub,
    IHttpClientFactory httpClientFactory,
    IOptions<PieHostOptions> pieHostOptions,
    ILogger<NotificadorIncidencias> logger) : INotificadorIncidencias
{
    public const string NombreHttpClient = "PieHost";

    public async Task NotificarAsync(IncidenciaNotificacion notificacion, CancellationToken ct = default)
    {
        try
        {
            await hub.Clients.All.SendAsync(IncidenciasHub.EventoIncidencia, notificacion, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo enviar por SignalR la notificación de la incidencia {Id}.", notificacion.Id);
        }

        var pieHost = pieHostOptions.Value;
        if (!pieHost.PublicacionHabilitada)
            return;

        try
        {
            var cliente = httpClientFactory.CreateClient(NombreHttpClient);
            using var respuesta = await cliente.PostAsJsonAsync(
                $"https://{pieHost.ClusterId}.piesocket.com/api/publish",
                new
                {
                    key = pieHost.ApiKey,
                    secret = pieHost.ApiSecret,
                    channelId = pieHost.Canal,
                    message = notificacion
                },
                ct);

            if (!respuesta.IsSuccessStatusCode)
                logger.LogWarning("PieHost respondió {Status} al publicar la incidencia {Id}.", (int)respuesta.StatusCode, notificacion.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo publicar en PieHost la incidencia {Id}.", notificacion.Id);
        }
    }
}
