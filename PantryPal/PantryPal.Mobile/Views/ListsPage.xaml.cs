using System.Collections.ObjectModel;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace PantryPal.Mobile.Views;

public partial class ListsPage : ContentPage
{
    private IListsService? _lists;
    private ISeedService? _seed;
    private ILogger<ListsPage>? _log;

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

        _log ??= ServiceHelper.Get<ILogger<ListsPage>>();
        _lists ??= ServiceHelper.Get<IListsService>();
        _seed ??= ServiceHelper.Get<ISeedService>();

        _log.LogInformation("[ListsPage] Appearing");
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _all = (await _lists!.GetAllAsync()).ToList();
            _log?.LogInformation("[ListsPage] Loaded count={Count}", _all.Count);
            ApplyFilter(SearchBar.Text);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Load failed");
            await DisplayAlert("Error", "Could not load lists.", "OK");
            _view.Clear();
        }
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
        try
        {
            var name = await DisplayPromptAsync("New List", "Enter a name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            await _lists!.CreateAsync(name.Trim());
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Create failed");
            await DisplayAlert("Error", "Could not create list.", "OK");
        }
    }

    private async void OnSeedClicked(object sender, EventArgs e)
    {
        try
        {
            // Optionally prompt for a name; or pass null to auto-name
            var list = await _seed!.CreateSampleListAsync(name: null, itemCount: null);
            _log?.LogInformation("[ListsPage] Seeded list id={Id} name='{Name}'", list.Id, list.Name);

            await LoadAsync();

            // Optional: jump straight into the new list
            var go = await DisplayActionSheet($"Seeded '{list.Name}'", "Stay", null, "Open");
            if (go == "Open")
            {
                await Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?ListId={list.Id}");
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Seeding failed");
            await DisplayAlert("Error", "Could not seed test data.", "OK");
        }
    }

    private async void OnRenameClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not GroceryList list) return;

        try
        {
            var newName = await DisplayPromptAsync("Rename", "New name:", initialValue: list.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            await _lists!.RenameAsync(list.Id, newName.Trim());
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Rename failed id={Id}", list.Id);
            await DisplayAlert("Error", "Could not rename list.", "OK");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not GroceryList list) return;

        try
        {
            var ok = await DisplayAlert("Delete", $"Delete '{list.Name}'?", "Delete", "Cancel");
            if (!ok) return;

            await _lists!.DeleteAsync(list.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Delete failed id={Id}", list.Id);
            await DisplayAlert("Error", "Could not delete list.", "OK");
        }
    }

    private async void OnListSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is GroceryList list)
            {
                await Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?ListId={list.Id}");
                ((CollectionView)sender).SelectedItem = null;
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Navigation to ListDetail failed");
            await DisplayAlert("Error", "Navigation failed.", "OK");
        }
    }
}
