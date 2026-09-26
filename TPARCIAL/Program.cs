using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;
using TPARCIAL.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
           .AddInterceptors(sp.GetRequiredService<InvalidarCacheIncidenciasInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

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
builder.Services.AddSingleton<InvalidarCacheIncidenciasInterceptor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
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

app.Run();
