using PantryPal.Core.Data;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace PantryPal.Core.Services;

public sealed class ListsService : IListsService
{
    private readonly PantryDatabase _db;
    private readonly IListItemsService _items;                
    private readonly ILogger<ListsService> _logger;

    public ListsService(PantryDatabase db, IListItemsService items, ILogger<ListsService> logger)
    {
        _db = db;
        _items = items;                                       
        _logger = logger;
    }

    public async Task<IReadOnlyList<GroceryList>> GetAllAsync()
    {
        try
        {
            var lists = await _db.Connection.Table<GroceryList>()
                .OrderByDescending(l => l.CreatedUtc)
                .ToListAsync();
            _logger.LogInformation("[ListsService] GetAllAsync count={Count}", lists.Count);
            return lists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] GetAllAsync failed");
            throw;
        }
    }

    public async Task<GroceryList?> GetAsync(int id)
    {
        try
        {
            var result = await _db.Connection.Table<GroceryList>()
                .Where(l => l.Id == id)
                .FirstOrDefaultAsync();
            _logger.LogInformation("[ListsService] GetAsync id={Id} found={Found}", id, result != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] GetAsync id={Id} failed", id);
            throw;
        }
    }

    
    public async Task<GroceryList> CreateAsync(string name, DateTime? createdUtc = null, DateTime? purchasedUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("List name is required.", nameof(name));

        try
        {
            var entity = new GroceryList
            {
                Name = name.Trim(),
                CreatedUtc = createdUtc ?? DateTime.UtcNow,
                PurchasedUtc = purchasedUtc
            };
            await _db.Connection.InsertAsync(entity);
            _logger.LogInformation("[ListsService] CreateAsync name='{Name}' id={Id} created={Created:u} purchased={Purchased:u}",
                entity.Name, entity.Id, entity.CreatedUtc, entity.PurchasedUtc);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] CreateAsync name='{Name}' failed", name);
            throw;
        }
    }

    public async Task RenameAsync(int id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name is required.", nameof(newName));

        try
        {
            var list = await GetAsync(id) ?? throw new InvalidOperationException("List not found.");
            var old = list.Name;
            list.Name = newName.Trim();
            await _db.Connection.UpdateAsync(list);
            _logger.LogInformation("[ListsService] RenameAsync id={Id} '{Old}' -> '{New}'", id, old, list.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] RenameAsync id={Id} failed", id);
            throw;
        }
    }

    
    public async Task UpdateAsync(int id, string name, DateTime? purchasedUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        try
        {
            var list = await GetAsync(id) ?? throw new InvalidOperationException("List not found.");

            var oldName = list.Name;
            var oldPurchased = list.PurchasedUtc;

            list.Name = name.Trim();
            list.PurchasedUtc = purchasedUtc;

            await _db.Connection.UpdateAsync(list);

            
            await _items.SetPurchasedForListItemsAsync(list.Id, purchasedUtc);

            _logger.LogInformation("[ListsService] UpdateAsync id={Id} name '{Old}' -> '{New}', purchased {OldDate:u} -> {NewDate:u}",
                id, oldName, list.Name, oldPurchased, purchasedUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] UpdateAsync id={Id} failed", id);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var rows = await _db.Connection.DeleteAsync<GroceryList>(id);
            if (rows == 0) throw new InvalidOperationException("List not found.");
            _logger.LogInformation("[ListsService] DeleteAsync id={Id} rows={Rows}", id, rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] DeleteAsync id={Id} failed", id);
            throw;
        }
    }

    // Single group query for lists summaries including empty lists
    public async Task<IReadOnlyList<ListSummary>> GetListSummariesAsync()
    {
        try
        {
            const string sql = @"
SELECT
  l.Id                  AS Id,
  l.Name                AS Name,
  l.CreatedUtc          AS CreatedUtc,
  l.PurchasedUtc        AS PurchasedUtc,
  COUNT(i.Id)           AS ItemCount,
  IFNULL(SUM(i.Cost),0) AS TotalCost
FROM GroceryLists l
LEFT JOIN GroceryListItems i ON i.ListId = l.Id
GROUP BY l.Id
ORDER BY l.CreatedUtc DESC;";

            var rows = await _db.Connection.QueryAsync<ListSummary>(sql);
            _logger.LogInformation("[ListsService] GetListSummaries count={Count}", rows.Count);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ListsService] GetListSummaries failed");
            throw;
        }
    }
}
