using SQLite;

namespace PantryPal.Core.Models;

[Table("GroceryLists")]
public class GroceryList
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = "";

    [NotNull]
    public DateTime CreatedUtc { get; set; }

    public DateTime? PurchasedUtc { get; set; }
}
