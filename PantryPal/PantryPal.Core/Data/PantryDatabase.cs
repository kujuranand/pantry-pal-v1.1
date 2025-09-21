using SQLite;
using PantryPal.Core.Migrations;

namespace PantryPal.Core.Data;

/// <summary>
/// Wraps a single SQLiteAsyncConnection and runs versioned migrations.
/// Pass in the DB file path from the app (Android) layer.
/// </summary>
public sealed class PantryDatabase
{
    private readonly string _dbPath;
    private readonly SQLiteOpenFlags _openFlags;
    private SQLiteAsyncConnection? _conn;
    private bool _initialized;

    // Register your migrations here in order
    private static readonly IMigration[] _migrations = new IMigration[]
    {
        new _0001_Initial(),
    };

    public PantryDatabase(string dbPath,
        SQLiteOpenFlags openFlags = SQLiteOpenFlags.ReadWrite
                                   | SQLiteOpenFlags.Create
                                   | SQLiteOpenFlags.SharedCache
                                   | SQLiteOpenFlags.FullMutex)
    {
        _dbPath = dbPath;
        _openFlags = openFlags;
    }

    public SQLiteAsyncConnection Connection =>
        _conn ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _conn = new SQLiteAsyncConnection(_dbPath, _openFlags);

        // Enforce FKs for this connection (also set inside migrations)
        await _conn.ExecuteAsync("PRAGMA foreign_keys = ON;");

        // Read current schema version
        var version = await _conn.ExecuteScalarAsync<int>("PRAGMA user_version;");

        // Apply migrations in sequence
        while (true)
        {
            var next = _migrations.FirstOrDefault(m => m.FromVersion == version);
            if (next is null) break;

            await next.UpAsync(_conn);
            version = next.ToVersion;

            // Persist new version
            await _conn.ExecuteAsync($"PRAGMA user_version = {version};");
        }

        _initialized = true;
    }
}
