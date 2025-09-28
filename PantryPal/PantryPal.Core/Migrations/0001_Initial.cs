using System;
using SQLite;
using PantryPal.Core.Models;
using Microsoft.Extensions.Logging;

namespace PantryPal.Core.Migrations;

internal sealed class _0001_Initial : IMigration
{
    public int FromVersion => 0;
    public int ToVersion => 1;

    public async Task UpAsync(SQLiteAsyncConnection conn)
    {
        ILogger? log = null;

        try
        {
            await conn.ExecuteAsync("PRAGMA foreign_keys = ON;");

            // lists
            log?.LogInformation("[Migration 0001] Create GroceryLists");
            await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS GroceryLists (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  Name          TEXT    NOT NULL,
  CreatedUtc    TEXT    NOT NULL,
  PurchasedUtc  TEXT    NOT NULL,
  Notes         TEXT    NULL
);");

            // indexes for sorting/filtering
            await conn.ExecuteAsync(@"
CREATE INDEX IF NOT EXISTS IX_GroceryLists_CreatedUtc
ON GroceryLists(CreatedUtc);");

            await conn.ExecuteAsync(@"
CREATE INDEX IF NOT EXISTS IX_GroceryLists_PurchasedUtc
ON GroceryLists(PurchasedUtc);");

            // items
            log?.LogInformation("[Migration 0001] Create GroceryListItems");
            await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS GroceryListItems (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  ListId        INTEGER NOT NULL,
  Name          TEXT    NOT NULL,
  Cost          NUMERIC NOT NULL,
  PurchasedDate TEXT,
  FOREIGN KEY(ListId) REFERENCES GroceryLists(Id) ON DELETE CASCADE
);");

            // index
            log?.LogInformation("[Migration 0001] Create index IX_GroceryListItems_ListId");
            await conn.ExecuteAsync(@"
CREATE INDEX IF NOT EXISTS IX_GroceryListItems_ListId
ON GroceryListItems(ListId);");

            await conn.CreateTableAsync<GroceryList>();
            await conn.CreateTableAsync<GroceryListItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Migration 0001] Failed: {ex}");
            throw;
        }
    }
}
