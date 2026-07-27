using MediaForge.GenZ.Core.Contracts;
using MediaForge.Universal.Services;
using MediaForge.Universal.ViewModels;
using MediaForge.Universal.Views;
using Microsoft.Extensions.Logging;
#if ANDROID
using MediaForge.Universal.Platforms.Android.Services;
#endif

namespace MediaForge.Universal;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IMediaImportService, SystemMediaImportService>();
#if ANDROID
        builder.Services.AddSingleton<IMetadataReader, AndroidMediaMetadataReader>();
        builder.Services.AddSingleton<IMediaPreviewService, AndroidMediaPreviewService>();
#else
        builder.Services.AddSingleton<FallbackMediaInspector>();
        builder.Services.AddSingleton<IMetadataReader>(
            services => services.GetRequiredService<FallbackMediaInspector>());
        builder.Services.AddSingleton<IMediaPreviewService>(
            services => services.GetRequiredService<FallbackMediaInspector>());
#endif
        builder.Services.AddSingleton<MediaDetailsViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
