using MediaForge.Universal.Views;

namespace MediaForge.Universal;

public partial class AppShell : Shell
{
    public AppShell(HomePage homePage)
    {
        InitializeComponent();
        HomeContent.Content = homePage;
    }
}
