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

    private List<ListSummary> _all = new();
    private readonly ObservableCollection<DateGroup> _groups = new();

    private bool _suppressNextTap;
    private static readonly TimeSpan TapSuppression = TimeSpan.FromMilliseconds(150);

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

    private GroupKey _groupKey = GroupKey.Created;

    public ICommand LongPressCommand { get; }

    public ListsPage()
    {
        InitializeComponent();
        BindingContext = this;
        ListsView.ItemsSource = _groups;
        LongPressCommand = new Command<ListSummary>(async s => await OnCardLongPressAsync(s));
        SortPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _log ??= ServiceHelper.Get<ILogger<ListsPage>>();
        _lists ??= ServiceHelper.Get<IListsService>();
        _seed ??= ServiceHelper.Get<ISeedService>();

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _all = (await _lists!.GetListSummariesAsync()).ToList();
            ApplyFilter(SearchBar.Text);
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Could not load lists.", "OK");
            _groups.Clear();
            UpdateAllTotal(Array.Empty<ListSummary>());
            UpdateEmptyState(null, 0);
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

        IEnumerable<IGrouping<DateTime, ListSummary>> grouped;

        if (_groupKey == GroupKey.Created)
        {
            grouped = filtered
                .GroupBy(s => s.CreatedUtc.ToLocalTime().Date)
                .OrderByDescending(g => g.Key);
        }
        else
        {
            grouped = filtered
                .GroupBy(s => ((s.PurchasedUtc ?? s.CreatedUtc).ToLocalTime().Date))
                .OrderByDescending(g => g.Key);
        }

        foreach (var g in grouped)
        {
            var group = new DateGroup(g.Key);
            IEnumerable<ListSummary> ordered = _groupKey == GroupKey.Created
                ? g.OrderByDescending(i => i.CreatedUtc)
                : g.OrderByDescending(i => i.PurchasedUtc ?? i.CreatedUtc);

            foreach (var item in ordered)
                group.Items.Add(item);

            _groups.Add(group);
        }
    }

    private void UpdateAllTotal(IEnumerable<ListSummary> filtered)
    {
        var total = filtered.Sum(s => s.TotalCost);
        var count = filtered.Count();

        AllTotalCaptionText = count == 1 ? "Total (1 list)" : $"Total ({count} lists)";
        AllTotalValueText = $"${total:0.00}";
    }

    private void UpdateEmptyState(string? query, int filteredCount)
    {
        var isTrueEmpty = filteredCount == 0 && string.IsNullOrWhiteSpace(query);
        EmptyStateView.IsVisible = isTrueEmpty;
        ListsView.IsVisible = !isTrueEmpty;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);

    private void OnSortChanged(object? sender, EventArgs e)
    {
        var idx = SortPicker.SelectedIndex;
        _groupKey = idx == 1 ? GroupKey.Purchased : GroupKey.Created;
        ApplyFilter(SearchBar.Text);
    }

    private async void OnNewList(object sender, EventArgs e)
    {
        try
        {
            var name = await DisplayPromptAsync("New List", "Enter a name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            await _lists!.CreateAsync(name.Trim());
            await LoadAsync();
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Could not create list.", "OK");
        }
    }

    private async void OnSeedClicked(object sender, EventArgs e)
    {
        try
        {
            var list = await _seed!.CreateSampleListAsync(name: null, itemCount: null);
            await LoadAsync();

            var go = await DisplayActionSheet($"Seeded '{list.Name}'", "Stay", null, "Open");
            if (go == "Open")
            {
                await Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?ListId={list.Id}");
            }
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Could not seed test data.", "OK");
        }
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_suppressNextTap) return;

            var contextItem = (sender as BindableObject)?.BindingContext as ListSummary;
            var paramItem = e.Parameter as ListSummary;
            var summary = contextItem ?? paramItem;
            if (summary is null) return;

            await Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?ListId={summary.Id}");
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Navigation failed.", "OK");
        }
    }

    private async Task OnCardLongPressAsync(ListSummary s)
    {
        try
        {
            _suppressNextTap = true;

            var choice = await DisplayActionSheet(
                $"Options for '{s.Name}'",
                "Cancel", null,
                "Edit", "Delete");

            if (choice == "Edit")
            {
                await Shell.Current.GoToAsync($"{nameof(ListEditPage)}?ListId={s.Id}");
            }
            else if (choice == "Delete")
            {
                await DeleteAsync(s);
            }
        }
        finally
        {
            await Task.Delay(TapSuppression);
            _suppressNextTap = false;
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
        catch (Exception)
        {
            await DisplayAlert("Error", "Could not delete list.", "OK");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private enum GroupKey { Created, Purchased }

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
        public string DisplayDate { get; }
        public ObservableCollection<ListSummary> Items { get; } = new();
    }
}
