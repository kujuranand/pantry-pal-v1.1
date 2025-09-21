using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile.Views;

public partial class ListsPage : ContentPage
{
    private IListsService? _lists; // resolve later
    private readonly ObservableCollection<GroceryList> _view = new();
    private List<GroceryList> _all = new();

    public ListsPage()
    {
        InitializeComponent();
        ListsView.ItemsSource = _view;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _lists ??= ServiceHelper.Get<IListsService>();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _all = (await _lists!.GetAllAsync()).ToList();
        ApplyFilter(SearchBar.Text);
    }

    private void ApplyFilter(string? q)
    {
        q = (q ?? string.Empty).Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(l => l.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _view.Clear();
        foreach (var l in filtered) _view.Add(l);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);

    private async void OnNewList(object sender, EventArgs e)
    {
        if (_lists is null) return;
        var name = await DisplayPromptAsync("New List", "Enter a name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        await _lists.CreateAsync(name.Trim());
        await LoadAsync();
    }

    private async void OnRenameClicked(object sender, EventArgs e)
    {
        if (_lists is null) return;
        if ((sender as Button)?.CommandParameter is not GroceryList list) return;

        var newName = await DisplayPromptAsync("Rename", "New name:", initialValue: list.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;

        await _lists.RenameAsync(list.Id, newName.Trim());
        await LoadAsync();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_lists is null) return;
        if ((sender as Button)?.CommandParameter is not GroceryList list) return;

        var ok = await DisplayAlert("Delete", $"Delete '{list.Name}'?", "Delete", "Cancel");
        if (!ok) return;

        await _lists.DeleteAsync(list.Id);
        await LoadAsync();
    }

    private async void OnListSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is GroceryList list)
        {
            await Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?ListId={list.Id}");
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
