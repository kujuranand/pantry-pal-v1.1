using PantryPal.Core.Data;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;

namespace PantryPal.Core.Services;

public sealed class ListItemsService : IListItemsService
{
    private readonly PantryDatabase _db;

    public ListItemsService(PantryDatabase db) => _db = db;

    public async Task<IReadOnlyList<GroceryListItem>> GetByListAsync(int listId) =>
        await _db.Connection.Table<GroceryListItem>()
            .Where(i => i.ListId == listId)
            .OrderByDescending(i => i.PurchasedDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync();

    public async Task<GroceryListItem?> GetAsync(int id)
    {
        var result = await _db.Connection.Table<GroceryListItem>()
            .Where(i => i.Id == id)
            .FirstOrDefaultAsync();
        return result; // may be null
    }

    public async Task AddOrUpdateAsync(GroceryListItem item)
    {
        if (item.ListId <= 0) throw new ArgumentException("ListId is required.", nameof(item));
        if (string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("Name is required.", nameof(item));
        if (item.Cost < 0) throw new ArgumentException("Cost must be >= 0.", nameof(item));

        if (item.Id == 0)
            await _db.Connection.InsertAsync(item);
        else
            await _db.Connection.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        var rows = await _db.Connection.DeleteAsync<GroceryListItem>(id);
        if (rows == 0) throw new InvalidOperationException("Item not found.");
    }

    public async Task<decimal> GetTotalCostAsync(int listId)
    {
        var sum = await _db.Connection.ExecuteScalarAsync<double>(
            "SELECT IFNULL(SUM(Cost), 0) FROM GroceryListItems WHERE ListId = ?", listId);
        return (decimal)sum;
    }
}
