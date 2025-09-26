using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;                 
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

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

    // Suppress accidental tap after long-press
    private bool _suppressNextTap;
    private static readonly TimeSpan TapSuppression = TimeSpan.FromMilliseconds(150);

    // Prevent double navigations
    private bool _navigating;

    // Long-press command (used by TouchBehavior in XAML)
    public ICommand LongPressCommand { get; }

    // Bindable total bar text (matches Lists page style)
    private string _totalCaptionText = "Total (0 items)";
    public string TotalCaptionText
    {
        get => _totalCaptionText;
        set
        {
            if (_totalCaptionText != value)
            {
                _totalCaptionText = value;
                OnPropertyChanged();
            }
        }
    }

    private string _totalValueText = "$0.00";
    public string TotalValueText
    {
        get => _totalValueText;
        set
        {
            if (_totalValueText != value)
            {
                _totalValueText = value;
                OnPropertyChanged();
            }
        }
    }

    public ListDetailPage()
    {
        InitializeComponent();
        BindingContext = this;                       // enable total bar bindings
        ItemsView.ItemsSource = _view;

        LongPressCommand = new Command<GroceryListItem>(async i => await OnItemLongPressAsync(i));
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
            _view.Clear();
            UpdateTotalBar();
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

        UpdateTotalBar();

        _log?.LogInformation("[ListDetailPage] Filter query='{Query}' items={Count} total={Total}",
            q, filtered.Count, _view.Sum(i => i.Cost));
    }

    private void UpdateTotalBar()
    {
        var itemCount = _view.Count;
        var total = _view.Sum(i => i.Cost);
        TotalCaptionText = itemCount == 1 ? "Total (1 item)" : $"Total ({itemCount} items)";
        TotalValueText = $"${total:0.00}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);

    private async void OnAddItem(object sender, EventArgs e)
    {
        try
        {
            if (_navigating) return;
            _navigating = true;

            // Use querystring so [QueryProperty] continues to work
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}")
            );
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Navigation to ItemEdit (add) failed");
            await DisplayAlert("Error", $"Navigation failed.\n{ex.Message}", "OK");
        }
        finally
        {
            await Task.Delay(100);
            _navigating = false;
        }
    }

    // Tap: open editor (respects suppression and nav fence)
    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_suppressNextTap || _navigating)
            {
                _log?.LogInformation("[ListDetailPage] Tap ignored (suppressed={Suppressed}, navigating={Navigating})",
                    _suppressNextTap, _navigating);
                return;
            }

            // Resolve from sender's BindingContext first (robust with virtualization)
            var ctxItem = (sender as BindableObject)?.BindingContext as GroceryListItem;
            var paramItem = e.Parameter as GroceryListItem;
            var item = ctxItem ?? paramItem;

            if (item is null)
            {
                _log?.LogWarning("[ListDetailPage] OnCardTapped: null item");
                return;
            }

            _navigating = true;
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}&ItemId={item.Id}")
            );
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Navigation to ItemEdit (tap) failed");
            await DisplayAlert("Error", $"Navigation failed.\n{ex.Message}", "OK");
        }
        finally
        {
            await Task.Delay(100);
            _navigating = false;
        }
    }

    // Long-press: ActionSheet (Edit/Delete) + suppress following tap
    private async Task OnItemLongPressAsync(GroceryListItem item)
    {
        try
        {
            _suppressNextTap = true;

            var choice = await DisplayActionSheet(
                $"Options for '{item.Name}'",
                "Cancel", null,
                "Edit", "Delete");

            if (choice == "Edit")
            {
                if (_navigating) return;
                _navigating = true;

                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current.GoToAsync($"{nameof(ItemEditPage)}?ListId={ListId}&ItemId={item.Id}")
                );
            }
            else if (choice == "Delete")
            {
                var ok = await DisplayAlert("Delete", $"Delete '{item.Name}'?", "Delete", "Cancel");
                if (!ok) return;

                await _items!.DeleteAsync(item.Id);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListDetailPage] Long-press action failed id={Id}", item.Id);
            await DisplayAlert("Error", $"Action failed.\n{ex.Message}", "OK");
        }
        finally
        {
            await Task.Delay(TapSuppression);
            _suppressNextTap = false;
            _navigating = false;
        }
    }
}
