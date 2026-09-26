using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TPARCIAL.Models;

namespace TPARCIAL.Services;

public interface IIncidenciasCache
{
    /// <summary>Devuelve el listado desde la caché; si no está (o Redis falla) lo carga desde la BD.</summary>
    Task<List<Incidencia>> ObtenerListadoAsync(Func<CancellationToken, Task<List<Incidencia>>> cargarDesdeBd, CancellationToken ct = default);

    /// <summary>Elimina el listado cacheado para que la próxima lectura vaya a la BD.</summary>
    Task InvalidarAsync(CancellationToken ct = default);
}

/// <summary>
/// Caché distribuida (Redis) del listado de incidencias con degradación elegante:
/// cualquier fallo de Redis se registra y la aplicación sigue trabajando contra la base de datos.
/// </summary>
public class IncidenciasCache(IDistributedCache cache, ILogger<IncidenciasCache> logger) : IIncidenciasCache
{
    private const string ClaveListado = "incidencias:listado";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    // Tras un fallo, se deja de intentar usar Redis durante este tiempo para no penalizar cada petición.
    private static readonly TimeSpan PausaTrasFallo = TimeSpan.FromSeconds(30);
    private long _redisNoDisponibleHastaTicks;

    private static readonly DistributedCacheEntryOptions OpcionesEntrada = new()
    {
        AbsoluteExpirationRelativeToNow = Ttl
    };

    private bool RedisEnPausa => DateTime.UtcNow.Ticks < Interlocked.Read(ref _redisNoDisponibleHastaTicks);

    public async Task<List<Incidencia>> ObtenerListadoAsync(
        Func<CancellationToken, Task<List<Incidencia>>> cargarDesdeBd, CancellationToken ct = default)
    {
        if (!RedisEnPausa)
        {
            try
            {
                var datos = await cache.GetStringAsync(ClaveListado, ct);
                if (datos is not null)
                {
                    var cacheadas = JsonSerializer.Deserialize<List<Incidencia>>(datos);
                    if (cacheadas is not null)
                        return cacheadas;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RegistrarFallo(ex, "leer");
            }
        }

        var incidencias = await cargarDesdeBd(ct);

        if (!RedisEnPausa)
        {
            try
            {
                await cache.SetStringAsync(ClaveListado, JsonSerializer.Serialize(incidencias), OpcionesEntrada, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RegistrarFallo(ex, "guardar");
            }
        }

        return incidencias;
    }

    public async Task InvalidarAsync(CancellationToken ct = default)
    {
        try
        {
            // Se intenta aunque Redis esté "en pausa": una entrada obsoleta es peor que un intento fallido.
            await cache.RemoveAsync(ClaveListado, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RegistrarFallo(ex, "invalidar");
        }
    }

    private void RegistrarFallo(Exception ex, string operacion)
    {
        Interlocked.Exchange(ref _redisNoDisponibleHastaTicks, DateTime.UtcNow.Add(PausaTrasFallo).Ticks);
        logger.LogWarning(ex, "Redis no disponible al {Operacion} la caché de incidencias; se usa la base de datos.", operacion);
    }
}
