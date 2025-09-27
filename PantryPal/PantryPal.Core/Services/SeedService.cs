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
        // Name: "LIst N" unless caller provides one
        var listName = string.IsNullOrWhiteSpace(name)
            ? $"List {await GetNextTestIndexAsync()}"
            : name.Trim();

        // 3–10 items (or clamp caller's request)
        var count = itemCount.HasValue ? Math.Clamp(itemCount.Value, 3, 10) : _rng.Next(3, 11);

        // Pick a list date in the last ~90 days (3 months), midnight UTC
        var listDateUtc = RandomRecentUtc(daysBackInclusive: 90);

        _log.LogInformation("[Seed] Creating sample list '{Name}' on {Date:u} with {Count} items",
            listName, listDateUtc, count);

        // Create the list with our chosen CreatedUtc
        var list = await _lists.CreateAsync(listName, createdUtc: listDateUtc);

        // Add items (PurchasedDate = list date for consistent grouping)
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var itemName = NextUniqueName(used);
            var cost = RandomCost();

            var item = new GroceryListItem
            {
                ListId = list.Id,
                Name = itemName,
                Cost = cost,
                PurchasedDate = listDateUtc
            };

            try
            {
                await _items.AddOrUpdateAsync(item);
                _log.LogInformation("[Seed] Added item id={Id} name='{Name}' cost={Cost:0.00} purchased={Purchased:u}",
                    item.Id, item.Name, item.Cost, item.PurchasedDate);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[Seed] Failed to add item name='{Name}'", itemName);
                throw;
            }
        }

        _log.LogInformation("[Seed] Completed sample list id={Id} '{Name}' on {Date:u}", list.Id, list.Name, listDateUtc);
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
                const string prefix = "List ";
                if (l.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var tail = l.Name.Substring(prefix.Length).Trim();
                    if (int.TryParse(tail, out var n) && n > max)
                        max = n;
                }
            }

            var next = max + 1;
            _log.LogInformation("[Seed] Next 'List N' index computed as {Next}", next);
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
        for (int t = 0; t < 10; t++)
        {
            var name = SampleNames[_rng.Next(0, SampleNames.Length)];
            if (used.Add(name)) return name;
        }
        return $"Item {_rng.Next(100, 999)}";
    }

    private decimal RandomCost()
    {
        // $1.50–$25.00
        var cents = _rng.Next(150, 2501);
        return Math.Round(cents / 100m, 2);
    }

    private DateTime RandomRecentUtc(int daysBackInclusive)
    {
        // random day 0..daysBackInclusive in the past, at 00:00 UTC
        var back = _rng.Next(0, Math.Max(0, daysBackInclusive) + 1);
        return DateTime.UtcNow.Date.AddDays(-back);
    }
}
