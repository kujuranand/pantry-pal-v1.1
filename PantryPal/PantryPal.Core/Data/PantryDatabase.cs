using SQLite;
using PantryPal.Core.Migrations;
using Microsoft.Extensions.Logging;

namespace PantryPal.Core.Data;

public sealed class PantryDatabase
{
    private readonly string _dbPath;
    private readonly SQLiteOpenFlags _openFlags;
    private readonly ILogger<PantryDatabase> _logger;
    private SQLiteAsyncConnection? _conn;
    private bool _initialized;

    private static readonly IMigration[] _migrations = new IMigration[]
    {
        new _0001_Initial(),
    };

    public PantryDatabase(
        string dbPath,
        ILogger<PantryDatabase> logger,
        SQLiteOpenFlags openFlags = SQLiteOpenFlags.ReadWrite
                                  | SQLiteOpenFlags.Create
                                  | SQLiteOpenFlags.SharedCache
                                  | SQLiteOpenFlags.FullMutex)
    {
        _dbPath = dbPath;
        _openFlags = openFlags;
        _logger = logger;
    }

    public SQLiteAsyncConnection Connection =>
        _conn ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("[DB] Opening connection path='{Path}' flags='{Flags}'", _dbPath, _openFlags);

        try
        {
            _conn = new SQLiteAsyncConnection(_dbPath, _openFlags);

            await _conn.ExecuteAsync("PRAGMA foreign_keys = ON;");
            var version = await _conn.ExecuteScalarAsync<int>("PRAGMA user_version;");
            _logger.LogInformation("[DB] Current user_version={Version}", version);

            while (true)
            {
                var next = _migrations.FirstOrDefault(m => m.FromVersion == version);
                if (next is null) break;

                _logger.LogInformation("[DB] Applying migration {From}->{To}", next.FromVersion, next.ToVersion);
                try
                {
                    await next.UpAsync(_conn);
                    version = next.ToVersion;
                    await _conn.ExecuteAsync($"PRAGMA user_version = {version};");
                    _logger.LogInformation("[DB] Migration complete -> user_version={Version}", version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DB] Migration {From}->{To} failed", next.FromVersion, next.ToVersion);
                    throw;
                }
            }

            _initialized = true;
            _logger.LogInformation("[DB] Initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DB] InitializeAsync failed.");
            throw;
        }
    }
}
