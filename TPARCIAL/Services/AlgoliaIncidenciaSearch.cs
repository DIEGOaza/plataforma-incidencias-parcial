using System.Text.Json.Serialization;
using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.Extensions.Options;
using TPARCIAL.Models;

namespace TPARCIAL.Services;

public interface IIncidenciaSearch
{
    /// <summary>Devuelve los Id de incidencia que coinciden con el texto, en el orden de relevancia de Algolia.</summary>
    Task<IReadOnlyList<int>> BuscarIdsAsync(string texto, CancellationToken ct = default);

    /// <summary>Publica las incidencias en el índice (objectID = Id de la incidencia).</summary>
    Task SincronizarAsync(IEnumerable<Incidencia> incidencias, CancellationToken ct = default);
}

/// <summary>
/// Acceso a Algolia exclusivamente desde el servidor: la Admin API Key nunca llega a la vista ni al navegador.
/// </summary>
public class AlgoliaIncidenciaSearch(IOptions<AlgoliaOptions> options) : IIncidenciaSearch
{
    private const int MaxResultados = 100;

    private readonly AlgoliaOptions _options = options.Value;
    private SearchClient? _client;

    private SearchClient Client => _client ??= _options.EstaConfigurado
        ? new SearchClient(_options.ApplicationId, _options.AdminApiKey)
        : throw new InvalidOperationException(
            "Algolia no está configurado. Defina Algolia:ApplicationId y Algolia:AdminApiKey en user-secrets o variables de entorno.");

    public async Task<IReadOnlyList<int>> BuscarIdsAsync(string texto, CancellationToken ct = default)
    {
        var respuesta = await Client.SearchSingleIndexAsync<IncidenciaHit>(
            _options.IndexName,
            new SearchParams(new SearchParamsObject
            {
                Query = texto,
                HitsPerPage = MaxResultados,
                RestrictSearchableAttributes = [nameof(IncidenciaRegistro.NombreEstacion), nameof(IncidenciaRegistro.Descripcion)],
                AttributesToRetrieve = ["objectID"]
            }),
            cancellationToken: ct);

        return respuesta.Hits
            .Select(h => int.TryParse(h.ObjectID, out var id) ? id : (int?)null)
            .OfType<int>()
            .ToList();
    }

    public async Task SincronizarAsync(IEnumerable<Incidencia> incidencias, CancellationToken ct = default)
    {
        await Client.SetSettingsAsync(
            _options.IndexName,
            new IndexSettings
            {
                SearchableAttributes = [nameof(IncidenciaRegistro.NombreEstacion), nameof(IncidenciaRegistro.Descripcion)]
            },
            cancellationToken: ct);

        var registros = incidencias.Select(i => new IncidenciaRegistro
        {
            ObjectID = i.Id.ToString(),
            NombreEstacion = i.Estacion?.Nombre ?? string.Empty,
            Descripcion = i.Descripcion,
            Estado = i.Estado.ToString()
        });

        await Client.SaveObjectsAsync(_options.IndexName, registros, cancellationToken: ct);
    }

    private class IncidenciaHit
    {
        [JsonPropertyName("objectID")]
        public string ObjectID { get; set; } = string.Empty;
    }

    private class IncidenciaRegistro
    {
        [JsonPropertyName("objectID")]
        public string ObjectID { get; set; } = string.Empty;

        [JsonPropertyName(nameof(NombreEstacion))]
        public string NombreEstacion { get; set; } = string.Empty;

        [JsonPropertyName(nameof(Descripcion))]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName(nameof(Estado))]
        public string Estado { get; set; } = string.Empty;
    }
}
