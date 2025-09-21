using PantryPal.Core.Data;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace PantryPal.Core.Services;

public sealed class ListItemsService : IListItemsService
{
    private readonly PantryDatabase _db;
    private readonly ILogger<ListItemsService> _logger;

    public ListItemsService(PantryDatabase db, ILogger<ListItemsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GroceryListItem>> GetByListAsync(int listId)
    {
        try
        {
            var items = await _db.Connection.Table<GroceryListItem>()
                .Where(i => i.ListId == listId)
                .OrderByDescending(i => i.PurchasedDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();
            _logger.LogInformation("[ItemsService] GetByListAsync listId={ListId} count={Count}", listId, items.Count);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ItemsService] GetByListAsync listId={ListId} failed", listId);
            throw;
        }
    }

    public async Task<GroceryListItem?> GetAsync(int id)
    {
        try
        {
            var item = await _db.Connection.Table<GroceryListItem>()
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();
            _logger.LogInformation("[ItemsService] GetAsync id={Id} found={Found}", id, item != null);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ItemsService] GetAsync id={Id} failed", id);
            throw;
        }
    }

    public async Task AddOrUpdateAsync(GroceryListItem item)
    {
        if (item.ListId <= 0) throw new ArgumentException("ListId is required.", nameof(item));
        if (string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("Name is required.", nameof(item));
        if (item.Cost < 0) throw new ArgumentException("Cost must be >= 0.", nameof(item));

        try
        {
            if (item.Id == 0)
            {
                await _db.Connection.InsertAsync(item);
                _logger.LogInformation("[ItemsService] Insert listId={ListId} id={Id}", item.ListId, item.Id);
            }
            else
            {
                await _db.Connection.UpdateAsync(item);
                _logger.LogInformation("[ItemsService] Update id={Id}", item.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ItemsService] AddOrUpdateAsync id={Id} listId={ListId} failed", item.Id, item.ListId);
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var rows = await _db.Connection.DeleteAsync<GroceryListItem>(id);
            if (rows == 0) throw new InvalidOperationException("Item not found.");
            _logger.LogInformation("[ItemsService] Delete id={Id} rows={Rows}", id, rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ItemsService] Delete id={Id} failed", id);
            throw;
        }
    }

    public async Task<decimal> GetTotalCostAsync(int listId)
    {
        try
        {
            var sum = await _db.Connection.ExecuteScalarAsync<double>(
                "SELECT IFNULL(SUM(Cost), 0) FROM GroceryListItems WHERE ListId = ?", listId);
            var total = (decimal)sum;
            _logger.LogInformation("[ItemsService] Total listId={ListId} total={Total}", listId, total);
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ItemsService] GetTotalCostAsync listId={ListId} failed", listId);
            throw;
        }
    }
}
