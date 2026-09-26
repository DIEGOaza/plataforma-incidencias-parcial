using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TPARCIAL.Hubs;

namespace TPARCIAL.Services;

/// <summary>Payload del evento IncidenciaActualizada: únicamente Id y Estado.</summary>
public record IncidenciaActualizada(int Id, string Estado);

public interface INotificadorIncidencias
{
    Task NotificarAsync(IncidenciaActualizada evento, CancellationToken ct = default);
}

/// <summary>
/// Publica el evento IncidenciaActualizada en PieHost (si está configurado) y a los clientes conectados por SignalR.
/// Un fallo al notificar se registra, pero nunca interrumpe la operación que lo originó.
/// </summary>
public class NotificadorIncidencias(
    IHubContext<IncidenciasHub> hub,
    IHttpClientFactory httpClientFactory,
    IOptions<PieHostOptions> pieHostOptions,
    ILogger<NotificadorIncidencias> logger) : INotificadorIncidencias
{
    public const string NombreHttpClient = "PieHost";

    public async Task NotificarAsync(IncidenciaActualizada evento, CancellationToken ct = default)
    {
        await PublicarEnPieHostAsync(evento, ct);

        try
        {
            await hub.Clients.All.SendAsync(IncidenciasHub.EventoIncidencia, evento, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo enviar por SignalR {Evento} de la incidencia {Id}.", IncidenciasHub.EventoIncidencia, evento.Id);
        }
    }

    private async Task PublicarEnPieHostAsync(IncidenciaActualizada evento, CancellationToken ct)
    {
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
                    channelId = pieHost.CanalEfectivo,
                    // Formato {event, data} de PieSocket: el cliente filtra por el nombre del evento.
                    message = new { @event = IncidenciasHub.EventoIncidencia, data = evento }
                },
                ct);

            if (respuesta.IsSuccessStatusCode)
                logger.LogInformation("Publicado {Evento} en PieHost: Id={Id}, Estado={Estado}.", IncidenciasHub.EventoIncidencia, evento.Id, evento.Estado);
            else
                logger.LogWarning("PieHost respondió {Status} al publicar la incidencia {Id}.", (int)respuesta.StatusCode, evento.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo publicar en PieHost la incidencia {Id}.", evento.Id);
        }
    }
}
