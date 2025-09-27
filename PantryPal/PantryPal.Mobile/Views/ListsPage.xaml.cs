using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PantryPal.Core.Models;
using PantryPal.Core.Services.Abstractions;
using PantryPal.Mobile.Services;

namespace PantryPal.Mobile.Views;

public partial class ListsPage : ContentPage, INotifyPropertyChanged
{
    private IListsService? _lists;
    private ISeedService? _seed;
    private ILogger<ListsPage>? _log;

    // All summaries (flat)
    private List<ListSummary> _all = new();

    // UI: groups (one per date)
    private readonly ObservableCollection<DateGroup> _groups = new();

    private bool _suppressNextTap;
    private static readonly TimeSpan TapSuppression = TimeSpan.FromMilliseconds(150);

    // total cost bar
    private string _allTotalCaptionText = "Total";
    public string AllTotalCaptionText
    {
        get => _allTotalCaptionText;
        set { if (_allTotalCaptionText != value) { _allTotalCaptionText = value; OnPropertyChanged(); } }
    }

    private string _allTotalValueText = "$0.00";
    public string AllTotalValueText
    {
        get => _allTotalValueText;
        set { if (_allTotalValueText != value) { _allTotalValueText = value; OnPropertyChanged(); } }
    }

    public ICommand LongPressCommand { get; }

    public ListsPage()
    {
        InitializeComponent();
        BindingContext = this;

        // bind groups to the CollectionView
        ListsView.ItemsSource = _groups;

        LongPressCommand = new Command<ListSummary>(async s => await OnCardLongPressAsync(s));
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
            _all = (await _lists!.GetListSummariesAsync()).ToList();
            _log?.LogInformation("[ListsPage] Loaded summaries count={Count}", _all.Count);
            ApplyFilter(SearchBar.Text);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Load failed");
            await DisplayAlert("Error", "Could not load lists.", "OK");
            _groups.Clear();
            UpdateAllTotal(filtered: Array.Empty<ListSummary>());
            UpdateEmptyState(query: null, filteredCount: 0);
        }
    }

    private void ApplyFilter(string? q)
    {
        q = (q ?? string.Empty).Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(l => l.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        RebuildGroups(filtered);
        UpdateAllTotal(filtered);
        UpdateEmptyState(q, filtered.Count);
    }

    private void RebuildGroups(List<ListSummary> filtered)
    {
        _groups.Clear();

        // Group by LOCAL date for CreatedUtc (so "Today" matches device time)
        foreach (var g in filtered
                     .GroupBy(s => s.CreatedUtc.ToLocalTime().Date)
                     .OrderByDescending(g => g.Key))
        {
            var group = new DateGroup(g.Key);
            foreach (var item in g.OrderByDescending(i => i.CreatedUtc))
                group.Items.Add(item);

            _groups.Add(group);
        }

        _log?.LogInformation("[ListsPage] Groups rebuilt: {GroupCount} groups", _groups.Count);
    }

    private void UpdateAllTotal(IEnumerable<ListSummary> filtered)
    {
        try
        {
            var total = filtered.Sum(s => s.TotalCost);
            var count = filtered.Count();

            AllTotalCaptionText = count == 1 ? "Total (1 list)" : $"Total ({count} lists)";
            AllTotalValueText = $"${total:0.00}";
            _log?.LogInformation("[ListsPage] AllTotal updated count={Count} total={Total}", count, total);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] UpdateAllTotal failed");
            AllTotalCaptionText = "Total";
            AllTotalValueText = "$0.00";
        }
    }

    // Empty-state logic: show ONLY when no lists and no search text
    private void UpdateEmptyState(string? query, int filteredCount)
    {
        var isTrueEmpty = filteredCount == 0 && string.IsNullOrWhiteSpace(query);
        EmptyStateView.IsVisible = isTrueEmpty;
        ListsView.IsVisible = !isTrueEmpty;
        _log?.LogInformation("[ListsPage] EmptyState isTrueEmpty={Empty}", isTrueEmpty);
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
            var list = await _seed!.CreateSampleListAsync(name: null, itemCount: null);
            _log?.LogInformation("[ListsPage] Seeded list id={Id} name='{Name}'", list.Id, list.Name);

            await LoadAsync();

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

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_suppressNextTap)
            {
                _log?.LogInformation("[ListsPage] Tap suppressed after long-press.");
                return;
            }

            // row context is ListSummary (inside the BindableLayout)
            var contextItem = (sender as BindableObject)?.BindingContext as ListSummary;
            var paramItem = e.Parameter as ListSummary;
            var summary = contextItem ?? paramItem;

            if (summary is null)
            {
                _log?.LogWarning("[ListsPage] OnCardTapped: null item context");
                return;
            }

            _log?.LogInformation("[ListsPage] Tap open id={Id} name='{Name}'", summary.Id, summary.Name);
            await Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?ListId={summary.Id}");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Navigation to ListDetail failed");
            await DisplayAlert("Error", "Navigation failed.", "OK");
        }
    }

    private async Task OnCardLongPressAsync(ListSummary s)
    {
        try
        {
            _suppressNextTap = true;

            _log?.LogInformation("[ListsPage] Long-press id={Id} name='{Name}'", s.Id, s.Name);
            var choice = await DisplayActionSheet(
                $"Options for '{s.Name}'",
                "Cancel", null,
                "Edit name", "Delete");

            if (choice == "Edit name")
            {
                await RenameAsync(s);
            }
            else if (choice == "Delete")
            {
                await DeleteAsync(s);
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Long-press action failed id={Id}", s.Id);
        }
        finally
        {
            await Task.Delay(TapSuppression);
            _suppressNextTap = false;
        }
    }

    private async Task RenameAsync(ListSummary s)
    {
        try
        {
            var newName = await DisplayPromptAsync("Rename", "New name:", initialValue: s.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            await _lists!.RenameAsync(s.Id, newName.Trim());
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Rename failed id={Id}", s.Id);
            await DisplayAlert("Error", "Could not rename list.", "OK");
        }
    }

    private async Task DeleteAsync(ListSummary s)
    {
        try
        {
            var ok = await DisplayAlert("Delete", $"Delete '{s.Name}'?", "Delete", "Cancel");
            if (!ok) return;

            await _lists!.DeleteAsync(s.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[ListsPage] Delete failed id={Id}", s.Id);
            await DisplayAlert("Error", "Could not delete list.", "OK");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Simple group model (one card per date)
    private sealed class DateGroup
    {
        public DateGroup(DateTime localDate)
        {
            LocalDate = localDate.Date;
            DisplayDate = LocalDate == DateTime.Now.Date
                ? "Today"
                : LocalDate.ToString("dd-MM-yyyy");
        }

        public DateTime LocalDate { get; }
        public string DisplayDate { get; }                       // <-- used by XAML
        public ObservableCollection<ListSummary> Items { get; } = new();
    }
}
