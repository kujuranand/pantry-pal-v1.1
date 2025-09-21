using PantryPal.Mobile.Views;

namespace PantryPal.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(ListDetailPage), typeof(ListDetailPage));
        Routing.RegisterRoute(nameof(ItemEditPage), typeof(ItemEditPage));
    }
}
