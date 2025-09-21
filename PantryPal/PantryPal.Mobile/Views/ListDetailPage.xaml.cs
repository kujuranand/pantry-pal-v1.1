using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile.Views;

[QueryProperty(nameof(ListId), nameof(ListId))]
public partial class ListDetailPage : ContentPage
{
    public int ListId { get; set; }

    private IListItemsService? _items; // resolve later
    private IListsService? _lists;     // resolve later
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

        _items ??= ServiceHelper.Get<IListItemsService>();
        _lists ??= ServiceHelper.Get<IListsService>();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _list = await _lists!.GetAsync(ListId);
        Title = _list?.Name ?? "List";

        _all = (await _items!.GetByListAsync(ListId)).ToList();
        ApplyFilter(SearchBar.Text);
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
        await Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}");
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not GroceryListItem item) return;
        await Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}&ItemId={item.Id}");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_items is null) return;
        if ((sender as Button)?.CommandParameter is not GroceryListItem item) return;

        var ok = await DisplayAlert("Delete", $"Delete '{item.Name}'?", "Delete", "Cancel");
        if (!ok) return;

        await _items.DeleteAsync(item.Id);
        await LoadAsync();
    }

    private void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        ((CollectionView)sender).SelectedItem = null;
    }
}
