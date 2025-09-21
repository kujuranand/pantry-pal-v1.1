using PantryPal.Core.Models;

namespace PantryPal.Core.Services.Abstractions;

public interface ISeedService
{
    Task<GroceryList> CreateSampleListAsync(string? name = null, int? itemCount = null, CancellationToken ct = default);
}
