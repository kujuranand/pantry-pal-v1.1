using System.Collections.ObjectModel;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace PantryPal.Mobile.Views;

[QueryProperty(nameof(ListId), nameof(ListId))]
public partial class ListDetailPage : ContentPage
{
    public int ListId { get; set; }

    private IListItemsService? _items;
    private IListsService? _lists;
    private ILogger<ListDetailPage>? _log;

    private readonly ObservableCollection<GroceryListItem> _view = new();
    private List<GroceryListItem> _all = new();
    private GroceryList? _list;

    public ListDetailPage()
    {
        InitializeComponent();
        ItemsView.ItemsSource = _view;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _log ??= ServiceHelper.Get<ILogger<ListDetailPage>>();
        _items ??= ServiceHelper.Get<IListItemsService>();
        _lists ??= ServiceHelper.Get<IListsService>();

        _log.LogInformation("[ListDetailPage] Appearing listId={ListId}", ListId);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _list = await _lists!.GetAsync(ListId);
            if (_list is null)
            {
                _log?.LogWarning("[ListDetailPage] List not found id={Id}", ListId);
                await DisplayAlert("Missing", "List not found.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Title = _list.Name;
            _all = (await _items!.GetByListAsync(ListId)).ToList();
            ApplyFilter(SearchBar.Text);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Load failed listId={ListId}", ListId);
            await DisplayAlert("Error", "Could not load items.", "OK");
        }
    }

    private void ApplyFilter(string? q)
    {
        q = (q ?? string.Empty).Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _view.Clear();
        foreach (var i in filtered) _view.Add(i);

        var total = filtered.Sum(i => i.Cost);
        TotalLabel.Text = $"${total:0.00}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);

    private async void OnAddItem(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Navigation to ItemEdit (add) failed");
            await DisplayAlert("Error", "Navigation failed.", "OK");
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not GroceryListItem item) return;

        try
        {
            await Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}&ItemId={item.Id}");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Navigation to ItemEdit (edit) failed id={Id}", item.Id);
            await DisplayAlert("Error", "Navigation failed.", "OK");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not GroceryListItem item) return;

        try
        {
            var ok = await DisplayAlert("Delete", $"Delete '{item.Name}'?", "Delete", "Cancel");
            if (!ok) return;

            await _items!.DeleteAsync(item.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Delete failed id={Id}", item.Id);
            await DisplayAlert("Error", "Could not delete item.", "OK");
        }
    }

    private void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        ((CollectionView)sender).SelectedItem = null;
    }
}
