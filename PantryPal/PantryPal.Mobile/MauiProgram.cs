using Microsoft.Extensions.Logging;
using PantryPal.Core.Data;
using PantryPal.Core.Services;
using PantryPal.Core.Services.Abstractions;
using Microsoft.Maui.Storage;   // FileSystem
using System.IO;                // Path

namespace PantryPal.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Initialise SQLitePCLRaw (bundle_green uses device SQLite)
            SQLitePCL.Batteries_V2.Init();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Local database path (Android app sandbox)
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pantrypal.db3");

            // Core database + services
            builder.Services.AddSingleton(new PantryDatabase(dbPath));
            builder.Services.AddSingleton<IListsService, ListsService>();
            builder.Services.AddSingleton<IListItemsService, ListItemsService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Run migrations before the UI
            var db = app.Services.GetRequiredService<PantryDatabase>();
            db.InitializeAsync().GetAwaiter().GetResult();

            return app;
        }
    }
}
