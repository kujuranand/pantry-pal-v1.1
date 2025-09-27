using PantryPal.Core.Models;

namespace PantryPal.Core.Services.Abstractions;

public interface IListsService
{
    Task<IReadOnlyList<GroceryList>> GetAllAsync();
    Task<GroceryList?> GetAsync(int id);
    Task<GroceryList> CreateAsync(string name, DateTime? createdUtc = null, DateTime? purchasedUtc = null);
    Task RenameAsync(int id, string newName);
    Task UpdateAsync(int id, string name, DateTime? purchasedUtc);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<ListSummary>> GetListSummariesAsync();
}
