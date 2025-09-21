using PantryPal.Core.Data;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;

namespace PantryPal.Core.Services;

public sealed class ListsService : IListsService
{
    private readonly PantryDatabase _db;

    public ListsService(PantryDatabase db) => _db = db;

    public async Task<IReadOnlyList<GroceryList>> GetAllAsync()
        => await _db.Connection.Table<GroceryList>()
               .OrderByDescending(l => l.CreatedUtc)
               .ToListAsync();

    public Task<GroceryList?> GetAsync(int id)
        => _db.Connection.Table<GroceryList>()
               .Where(l => l.Id == id)
               .FirstOrDefaultAsync();

    public async Task<GroceryList> CreateAsync(string name, DateTime? createdUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("List name is required.", nameof(name));

        var entity = new GroceryList
        {
            Name = name.Trim(),
            CreatedUtc = (createdUtc ?? DateTime.UtcNow)
        };

        await _db.Connection.InsertAsync(entity);
        return entity;
    }

    public async Task RenameAsync(int id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name is required.", nameof(newName));

        var list = await GetAsync(id) ?? throw new InvalidOperationException("List not found.");
        list.Name = newName.Trim();
        await _db.Connection.UpdateAsync(list);
    }

    public async Task DeleteAsync(int id)
    {
        // FK cascade will delete items (ON DELETE CASCADE set in migration)
        var rows = await _db.Connection.DeleteAsync<GroceryList>(id);
        if (rows == 0)
            throw new InvalidOperationException("List not found.");
    }
}
