using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile.Views;

[QueryProperty(nameof(ListId), nameof(ListId))]
[QueryProperty(nameof(ItemId), nameof(ItemId))]
public partial class ItemEditPage : ContentPage
{
    public int ListId { get; set; }
    public int? ItemId { get; set; }

    private IListItemsService? _items; // resolve later
    private GroceryListItem? _editing;

    public ItemEditPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _items ??= ServiceHelper.Get<IListItemsService>();

        PurchasedPicker.Date = DateTime.Now.Date;

        if (ItemId is int id)
        {
            var listItems = await _items!.GetByListAsync(ListId);
            _editing = listItems.FirstOrDefault(i => i.Id == id);
            if (_editing is null)
            {
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
            Title = "Add Item";
            PurchasedCheck.IsChecked = true;
            PurchasedPicker.IsEnabled = true;
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
        if (_items is null) return;

        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Required", "Name is required.", "OK");
            return;
        }

        if (!decimal.TryParse(CostEntry.Text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) || cost < 0)
        {
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

        await _items.AddOrUpdateAsync(item);
        await Shell.Current.GoToAsync("..");
    }
}
