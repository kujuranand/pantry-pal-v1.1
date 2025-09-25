using Microsoft.Extensions.Logging;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;

namespace PantryPal.Core.Services;

public sealed class SeedService : ISeedService
{
    private readonly IListsService _lists;
    private readonly IListItemsService _items;
    private readonly ILogger<SeedService> _log;
    private readonly Random _rng = new Random();

    private static readonly string[] SampleNames = new[]
    {
        "Milk", "Bread", "Eggs", "Butter", "Cheddar Cheese", "Chicken Breast",
        "Apples", "Bananas", "Tomatoes", "Lettuce", "Rice", "Pasta",
        "Olive Oil", "Yogurt", "Orange Juice", "Coffee", "Tea", "Sugar", "Flour", "Cereal"
    };

    public SeedService(IListsService lists, IListItemsService items, ILogger<SeedService> log)
    {
        _lists = lists;
        _items = items;
        _log = log;
    }

    public async Task<GroceryList> CreateSampleListAsync(string? name = null, int? itemCount = null, CancellationToken ct = default)
    {
        // list name with increment
        var listName = string.IsNullOrWhiteSpace(name)
            ? $"Test {await GetNextTestIndexAsync()}"              
            : name.Trim();

        var count = itemCount.HasValue ? Math.Clamp(itemCount.Value, 3, 10) : _rng.Next(3, 11);

        _log.LogInformation("[Seed] Creating sample list '{Name}' with {Count} items", listName, count);

        // Create list
        var list = await _lists.CreateAsync(listName);

        // random picks
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var itemName = NextUniqueName(used);
            var cost = RandomCost();
            var purchased = RandomPurchasedDateUtc();

            var item = new GroceryListItem
            {
                ListId = list.Id,
                Name = itemName,
                Cost = cost,
                PurchasedDate = purchased
            };

            try
            {
                await _items.AddOrUpdateAsync(item);
                _log.LogInformation("[Seed] Added item id={Id} name='{Name}' cost={Cost:0.00} purchased={Purchased}",
                    item.Id, item.Name, item.Cost, item.PurchasedDate?.ToString("u"));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[Seed] Failed to add item name='{Name}'", itemName);
                throw;
            }
        }

        _log.LogInformation("[Seed] Completed sample list id={Id} '{Name}'", list.Id, list.Name);
        return list;
    }

    // determine the next incremental index
    private async Task<int> GetNextTestIndexAsync()
    {
        try
        {
            var all = await _lists.GetAllAsync();
            var max = 0;

            foreach (var l in all)
            {
                const string prefix = "Test ";
                if (l.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var tail = l.Name.Substring(prefix.Length).Trim();
                    if (int.TryParse(tail, out var n) && n > max)
                        max = n;
                }
            }

            var next = max + 1;
            _log.LogInformation("[Seed] Next 'Test N' index computed as {Next}", next);
            return next;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Seed] GetNextTestIndexAsync failed; defaulting to 1");
            return 1;
        }
    }

    private string NextUniqueName(HashSet<string> used)
    {
        // avoid duplicates
        for (int t = 0; t < 10; t++)
        {
            var name = SampleNames[_rng.Next(0, SampleNames.Length)];
            if (used.Add(name)) return name;
        }
        return $"Item {_rng.Next(100, 999)}";
    }

    private decimal RandomCost()
    {
        // 1.50 to 25.00
        var cents = _rng.Next(150, 2501);
        return Math.Round(cents / 100m, 2);
    }

    private DateTime? RandomPurchasedDateUtc()
    {
        // date within the last 7 days, else null
        if (_rng.NextDouble() < 0.7)
        {
            var daysBack = _rng.Next(0, 7);
            return DateTime.UtcNow.Date.AddDays(-daysBack);
        }
        return null;
    }
}
