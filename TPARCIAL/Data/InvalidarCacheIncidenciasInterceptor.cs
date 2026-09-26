using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TPARCIAL.Models;
using TPARCIAL.Services;

namespace TPARCIAL.Data;

/// <summary>
/// Invalida la caché del listado cuando se agrega, modifica o elimina una Incidencia (o una Estacion,
/// porque su nombre forma parte del listado). Se invalida después de guardar para no dejar datos obsoletos.
/// </summary>
public class InvalidarCacheIncidenciasInterceptor(IIncidenciasCache cache) : SaveChangesInterceptor
{
    private static readonly ConditionalWeakTable<DbContext, object> ContextosConCambios = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        MarcarSiHayCambios(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        MarcarSiHayCambios(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (DesmarcarSiHabiaCambios(eventData.Context))
            cache.InvalidarAsync().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (DesmarcarSiHabiaCambios(eventData.Context))
            await cache.InvalidarAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DesmarcarSiHabiaCambios(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DesmarcarSiHabiaCambios(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void MarcarSiHayCambios(DbContext? context)
    {
        if (context is null)
            return;

        var hayCambios = context.ChangeTracker.Entries()
            .Any(e => e.Entity is Incidencia or Estacion
                      && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (hayCambios)
            ContextosConCambios.AddOrUpdate(context, new object());
    }

    private static bool DesmarcarSiHabiaCambios(DbContext? context) =>
        context is not null && ContextosConCambios.Remove(context);
}
