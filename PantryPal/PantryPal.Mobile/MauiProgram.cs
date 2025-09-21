using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PantryPal.Core.Data;
using PantryPal.Core.Services;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;
using System.IO;

namespace PantryPal.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // startup
            SQLitePCL.Batteries_V2.Init();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(f =>
                {
                    f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            builder.Logging.AddDebug();
#endif

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pantrypal.db3");

            // register database with logger via factory
            builder.Services.AddSingleton<PantryDatabase>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PantryDatabase>>();
                return new PantryDatabase(dbPath, logger);
            });

            // services with logging via DI
            builder.Services.AddSingleton<IListsService, ListsService>();
            builder.Services.AddSingleton<IListItemsService, ListItemsService>();
            builder.Services.AddSingleton<ISeedService, SeedService>();

            // optional: global exception breadcrumbs (DEBUG)
#if DEBUG
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                System.Diagnostics.Debug.WriteLine($"[Global] UnhandledException: {e.ExceptionObject}");

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Global] UnobservedTaskException: {e.Exception}");
                e.SetObserved();
            };
#endif

            var app = builder.Build();

            // make DI available to static helpers
            ServiceHelper.Init(app.Services);

            // initialize DB on background thread to avoid deadlock
            try
            {
                var logger = app.Services.GetRequiredService<ILogger<MauiApp>>();
                logger.LogInformation("[Startup] App built. Starting DB init...");

                var db = app.Services.GetRequiredService<PantryDatabase>();
                System.Threading.Tasks.Task.Run(() => db.InitializeAsync())
                    .GetAwaiter().GetResult();

                logger.LogInformation("[Startup] DB init complete.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup] DB init failed: {ex}");
            }

            return app;
        }
    }
}
