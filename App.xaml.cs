namespace MediaForge.Universal;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _shell = services.GetRequiredService<AppShell>();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell);
}
