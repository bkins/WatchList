using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using WatchLists.ExtensionMethods;
using WatchLists.MVVM.ViewModels;
using WatchLists.Utilities;

namespace WatchLists.MVVM.Views;

public partial class LogsPage : ContentPage
{
    public LogsPage()
    {
        InitializeComponent();
        BindingContext = App.Current.Services.GetService<LogsViewModel>();

        OnReadLogFileClicked();
    }

    private async void OnReadLogFileClicked ()
    {
        // if (File.Exists(LogConfig.LogFilePath))
        // {
    //         File.WriteAllText(LogConfig.LogFilePath
    //                         , "Test file write");
    //         Log.Logger.Information("Test message at {Time}"
    //                              , DateTime.Now);
    //         await Task.Delay(1000); // give it time to flush
    //
    //         var content = await File.ReadAllTextAsync(LogConfig.LogFilePath);
    //         LogOutput.Text = string.IsNullOrWhiteSpace(content)
    //                 ? "Log file is empty."
    //                 : content;
    //     }
    //     else
    //     {
    //         LogOutput.Text = "Log file not found.";
    //     }
    }

    private async void OnCopyLogsClicked (object?   sender
                                        , EventArgs e)
    {
        if ( Avails.FileDoesNotExist(LogConfig.LogFilePath))
        {
            await DisplayAlert("Error"
                             , "Log file not found."
                             , "OK");

            return;
        }

        var content = await File.ReadAllTextAsync(LogConfig.LogFilePath);

        if (content.IsEmpytNullOrWhiteSpace())
        {
            await DisplayAlert("Empty"
                             , "No log messages to copy."
                             , "OK");

            return;
        }

        await Clipboard.SetTextAsync(content);
        await DisplayAlert("Copied"
                         , "Log messages copied to clipboard."
                         , "OK");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LogsViewModel viewModel)
        {
            viewModel.LoadLogs();
        }
    }
}
