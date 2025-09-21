using SQLite;

namespace PantryPal.Core.Models;

[Table("GroceryListItems")]
public class GroceryListItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // FK to GroceryLists.Id
    [Indexed, NotNull]
    public int ListId { get; set; }

    [NotNull]
    public string Name { get; set; } = "";

    [NotNull]
    public decimal Cost { get; set; }

    // Optional
    public DateTime? PurchasedDate { get; set; }
}
