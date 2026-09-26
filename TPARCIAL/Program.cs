using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TPARCIAL.Data;
using TPARCIAL.Hubs;
using TPARCIAL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Algolia: la AdminApiKey se obtiene de user-secrets o de la variable de entorno Algolia__AdminApiKey.
builder.Services.Configure<AlgoliaOptions>(builder.Configuration.GetSection(AlgoliaOptions.Seccion));
builder.Services.AddSingleton<IIncidenciaSearch, AlgoliaIncidenciaSearch>();

// Caché distribuida: Redis si hay cadena de conexión (ConnectionStrings:Redis / variable ConnectionStrings__Redis);
// si no, caché en memoria para que la app funcione igual en local.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        var config = ConfigurationOptions.Parse(redisConnection);
        config.AbortOnConnectFail = false; // no romper el arranque si Redis no está disponible
        config.ConnectTimeout = 2000;
        config.ConnectRetry = 1;
        config.BacklogPolicy = BacklogPolicy.FailFast; // fallar al instante si no hay conexión
        config.SyncTimeout = 2000;
        config.AsyncTimeout = 2000;
        options.ConfigurationOptions = config;
        options.InstanceName = "tparcial:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSingleton<IIncidenciasCache, IncidenciasCache>();

// Tiempo real: PieHost (y SignalR). El ApiSecret se lee de user-secrets o de la variable PieHost__ApiSecret.
builder.Services.AddSignalR();
builder.Services.Configure<PieHostOptions>(builder.Configuration.GetSection(PieHostOptions.Seccion));
builder.Services.AddHttpClient(NotificadorIncidencias.NombreHttpClient, c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<INotificadorIncidencias, NotificadorIncidencias>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Publica las incidencias en el índice de Algolia si hay credenciales configuradas.
    if (app.Services.GetRequiredService<IOptions<AlgoliaOptions>>().Value.EstaConfigurado)
    {
        try
        {
            var incidencias = await db.Incidencias.Include(i => i.Estacion).AsNoTracking().ToListAsync();
            await app.Services.GetRequiredService<IIncidenciaSearch>().SincronizarAsync(incidencias);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo sincronizar el índice de Algolia.");
        }
    }
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<IncidenciasHub>(IncidenciasHub.Ruta);

app.Run();
