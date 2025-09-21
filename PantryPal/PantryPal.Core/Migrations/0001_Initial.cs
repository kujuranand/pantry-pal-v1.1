using SQLite;
using PantryPal.Core.Models;

namespace PantryPal.Core.Migrations;

/// <summary>Initial schema: GroceryLists + GroceryListItems (trimmed fields).</summary>
internal sealed class _0001_Initial : IMigration
{
    public int FromVersion => 0;
    public int ToVersion => 1;

    public async Task UpAsync(SQLiteAsyncConnection conn)
    {
        // Ensure FK enforcement
        await conn.ExecuteAsync("PRAGMA foreign_keys = ON;");

        // Create tables (idempotent)
        await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS GroceryLists (
  Id          INTEGER PRIMARY KEY AUTOINCREMENT,
  Name        TEXT    NOT NULL,
  CreatedUtc  TEXT    NOT NULL
);");

        await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS GroceryListItems (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  ListId        INTEGER NOT NULL,
  Name          TEXT    NOT NULL,
  Cost          NUMERIC NOT NULL,
  PurchasedDate TEXT,
  FOREIGN KEY(ListId) REFERENCES GroceryLists(Id) ON DELETE CASCADE
);");

        // Helpful index
        await conn.ExecuteAsync(@"
CREATE INDEX IF NOT EXISTS IX_GroceryListItems_ListId
ON GroceryListItems(ListId);");

        // Keep sqlite-net's model metadata in sync (safe if tables already exist)
        await conn.CreateTableAsync<GroceryList>();
        await conn.CreateTableAsync<GroceryListItem>();
    }
}
