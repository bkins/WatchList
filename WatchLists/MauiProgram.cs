using Microsoft.Extensions.Logging;
using WatchLists.MVVM.ViewModels;
using WatchLists.Services;
using CommunityToolkit.Maui;
using WatchLists.MVVM.Views;
using WatchLists.DataAccess.Interfaces;
using WatchLists.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using WatchLists.Logger;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using WatchLists.Utilities;


namespace WatchLists;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>()
               .ConfigureFonts(fonts =>
               {
                   fonts.AddFont("OpenSans-Regular.ttf"
                               , "OpenSansRegular");
                   fonts.AddFont("OpenSans-Semibold.ttf"
                               , "OpenSansSemibold");
                   fonts.AddFont("MaterialIcons-Regular.ttf"
                               , "MaterialIcons");
               })
               .UseMauiCommunityToolkit();
        // // Configure Serilog FIRST
        // Log.Logger = new LoggerConfiguration()
        //              .MinimumLevel.Verbose() // Capture everything
        //              .WriteTo.File(LogConfig.LogFilePath
        //                          , rollingInterval: RollingInterval.Day
        //                          , retainedFileCountLimit: 7)
        //              .CreateLogger();
        //
        // // THEN register logging services
        // builder.Services.AddLogging(logging =>
        // {
        //     logging.ClearProviders();
        //     logging.AddSerilog(); // Now it actually references Log.Logger
        //     logging.AddProvider(new MemoryLoggerProvider());
        //     logging.SetMinimumLevel(LogLevel.Trace);
        // });

        // Load secrets.json
        var secretsStream = FileSystem.OpenAppPackageFileAsync("secrets.json")
                                      .GetAwaiter()
                                      .GetResult();

        var config = new ConfigurationBuilder()
                     .AddJsonStream(secretsStream)
                     .Build();

        builder.Configuration.AddConfiguration(config);

        // Register View Models - Singletons and Transients
        builder.Services.AddSingleton<WatchListViewModel>();
        builder.Services.AddTransient<EditWatchItemViewModel>();
        builder.Services.AddTransient<ApiTestViewModel>();
        builder.Services.AddTransient<ManageOptionsViewModel>();
        builder.Services.AddTransient<MovieDetailsPage>();
        builder.Services.AddTransient<MovieDetailsViewModel>();
        builder.Services.AddSingleton<LogsViewModel>();
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();


        // Register HTTP Clients (but without API keys yet)
        builder.Services.AddHttpClient<TmdbService>();
        builder.Services.AddHttpClient<OmdbService>();
        builder.Services.AddHttpClient<FmDbService>();
        builder.Services.AddHttpClient<Imdb236Service>();
        builder.Services.AddHttpClient<WatchmodeService>();
        builder.Services.AddHttpClient<StreamingAvailabilityService>();
        builder.Services.AddHttpClient<TvMazeService>();
        builder.Services.AddHttpClient<WikipediaMovieProvider>();

        // Register services
        builder.Services.AddSingleton<WatchListService>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<MovieDataAggregator>();
        builder.Services.AddSingleton<IMovieDataAggregator, MovieDataAggregator>();
        builder.Services.AddSingleton<IMovieDataProvider, TmdbService>();
        builder.Services.AddSingleton<IMovieDataProvider, OmdbService>();
        builder.Services.AddSingleton<IMovieDataProvider, FmDbService>();
        builder.Services.AddSingleton<IMovieDataProvider, Imdb236Service>();
        builder.Services.AddSingleton<IMovieDataProvider, WatchmodeService>();
        builder.Services.AddSingleton<IMovieDataProvider, StreamingAvailabilityService>();
        builder.Services.AddSingleton<IMovieDataProvider, TvMazeService>();
        builder.Services.AddSingleton<IMovieDataProvider, WikipediaMovieProvider>();
        builder.Services.AddSingleton<IMovieDataProvider, GoogleSearchFallbackProvider>();

        // Build the app to resolve services
        var app = builder.Build();

        return app;
    }

}
