using System.Globalization;
using Microsoft.Extensions.Logging;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile.Views;

[QueryProperty(nameof(ListId), nameof(ListId))]
public partial class ListEditPage : ContentPage
{
    public int ListId { get; set; }

    private IListsService? _lists;
    private ILogger<ListEditPage>? _log;
    private GroceryList? _list;

    public ListEditPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _log ??= ServiceHelper.Get<ILogger<ListEditPage>>();
        _lists ??= ServiceHelper.Get<IListsService>();

        try
        {
            _list = await _lists!.GetAsync(ListId);
            if (_list is null)
            {
                await DisplayAlert("Missing", "List not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Title = "Edit List";
            NameEntry.Text = _list.Name;

            if (_list.PurchasedUtc.HasValue)
            {
                PurchasedCheck.IsChecked = true;
                PurchasedPicker.IsEnabled = true;
                PurchasedPicker.Date = DateTime.SpecifyKind(_list.PurchasedUtc.Value, DateTimeKind.Utc)
                                                .ToLocalTime().Date;
            }
            else
            {
                PurchasedCheck.IsChecked = false;
                PurchasedPicker.IsEnabled = false;
                PurchasedPicker.Date = DateTime.Now.Date;
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListEditPage] Load failed listId={ListId}", ListId);
            await DisplayAlert("Error", "Could not load list.", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private void OnPurchasedChecked(object sender, CheckedChangedEventArgs e)
    {
        PurchasedPicker.IsEnabled = e.Value;
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

            DateTime? purchasedUtc = null;
            if (PurchasedCheck.IsChecked)
                purchasedUtc = DateTime.SpecifyKind(PurchasedPicker.Date, DateTimeKind.Local).ToUniversalTime();

            await _lists!.UpdateAsync(ListId, name, purchasedUtc);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListEditPage] Save failed listId={ListId}", ListId);
            await DisplayAlert("Error", "Could not save.", "OK");
        }
    }

    private async void OnCancel(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
