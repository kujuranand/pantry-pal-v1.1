using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;                    
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile.Views;

public partial class ItemEditPage : ContentPage, IQueryAttributable
{
    public int ListId { get; set; }
    public int? ItemId { get; set; }

    private IListItemsService? _items;
    private ILogger<ItemEditPage>? _log;
    private GroceryListItem? _editing;

    public ItemEditPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query is null) return;

        if (query.TryGetValue(nameof(ListId), out var listVal))
        {
            if (listVal is int li) ListId = li;
            else if (listVal is string ls && int.TryParse(ls, out var lip)) ListId = lip;
        }

        if (query.TryGetValue(nameof(ItemId), out var itemVal))
        {
            if (itemVal is int ii) ItemId = ii;
            else if (itemVal is string istring && int.TryParse(istring, out var iip)) ItemId = iip;
            else ItemId = null; 
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _log ??= ServiceHelper.Get<ILogger<ItemEditPage>>();
        _items ??= ServiceHelper.Get<IListItemsService>();

        // Default date
        PurchasedPicker.Date = DateTime.Now.Date;

        try
        {
            if (ItemId is int id)
            {
                // Edit mode
                var listItems = await _items!.GetByListAsync(ListId);
                _editing = listItems.FirstOrDefault(i => i.Id == id);
                if (_editing is null)
                {
                    _log?.LogWarning("[ItemEditPage] Item not found id={Id}", id);
                    await DisplayAlert("Not found", "Item not found.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                Title = "Edit Item";
                NameEntry.Text = _editing.Name;
                CostEntry.Text = _editing.Cost.ToString("0.##", CultureInfo.InvariantCulture);

                if (_editing.PurchasedDate.HasValue)
                {
                    PurchasedCheck.IsChecked = true;
                    PurchasedPicker.IsEnabled = true;
                    PurchasedPicker.Date = DateTime.SpecifyKind(_editing.PurchasedDate.Value, DateTimeKind.Utc)
                                                   .ToLocalTime().Date;
                }
                else
                {
                    PurchasedCheck.IsChecked = false;
                    PurchasedPicker.IsEnabled = false;
                }
            }
            else
            {
                // Add mode
                Title = "Add Item";
                PurchasedCheck.IsChecked = true;
                PurchasedPicker.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ItemEditPage] OnAppearing load failed listId={ListId} itemId={ItemId}", ListId, ItemId);
            await DisplayAlert("Error", "Could not load item.", "OK");
        }
    }

    private void OnPurchasedChecked(object sender, CheckedChangedEventArgs e)
    {
        PurchasedPicker.IsEnabled = e.Value;
    }

    private async void OnCancel(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSave(object sender, EventArgs e)
    {
        try
        {
            var name = NameEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await DisplayAlert("Required", "Name is required.", "OK");
                return;
            }

            if (!decimal.TryParse(CostEntry.Text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) || cost < 0)
            {
                _log?.LogWarning("[ItemEditPage] Invalid cost input '{Raw}'", CostEntry.Text);
                await DisplayAlert("Cost", "Enter a cost >= 0 (e.g., 3.80).", "OK");
                return;
            }

            DateTime? purchasedUtc = null;
            if (PurchasedCheck.IsChecked)
                purchasedUtc = DateTime.SpecifyKind(PurchasedPicker.Date, DateTimeKind.Local).ToUniversalTime();

            var item = _editing ?? new GroceryListItem { ListId = ListId };
            item.Name = name;
            item.Cost = cost;
            item.PurchasedDate = purchasedUtc;

            await _items!.AddOrUpdateAsync(item);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ItemEditPage] Save failed listId={ListId} itemId={ItemId}", ListId, ItemId);
            await DisplayAlert("Error", "Could not save item.", "OK");
        }
    }
}
