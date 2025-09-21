using PantryPal.Core.Models;

namespace PantryPal.Core.Services.Abstractions;

public interface IListItemsService
{
    Task<IReadOnlyList<GroceryListItem>> GetByListAsync(int listId);
    Task<GroceryListItem?> GetAsync(int id);
    Task AddOrUpdateAsync(GroceryListItem item);
    Task DeleteAsync(int id);
    Task<decimal> GetTotalCostAsync(int listId);
}
