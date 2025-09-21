using SQLite;

namespace PantryPal.Core.Migrations;

public interface IMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    Task UpAsync(SQLiteAsyncConnection conn);
}
