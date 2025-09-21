using Microsoft.Extensions.Logging;
using PantryPal.Core.Data;
using PantryPal.Core.Services;
using PantryPal.Core.Services.Abstractions;
using Microsoft.Maui.Storage;
using System.IO;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            SQLitePCL.Batteries_V2.Init();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(f =>
                {
                    f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pantrypal.db3");

            builder.Services.AddSingleton(new PantryDatabase(dbPath));
            builder.Services.AddSingleton<IListsService, ListsService>();
            builder.Services.AddSingleton<IListItemsService, ListItemsService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Build app
            var app = builder.Build();

            // Make services reachable early
            ServiceHelper.Init(app.Services);

            // Run DB migrations on a background thread to avoid deadlock
            try
            {
                var db = app.Services.GetRequiredService<PantryDatabase>();
                System.Threading.Tasks.Task.Run(() => db.InitializeAsync())
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB init failed: {ex}");
            }

            return app;
        }
    }
}
