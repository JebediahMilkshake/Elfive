using System.Windows;
using System.Windows.Threading;

namespace Elfive.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (MainWindow is MainWindow main)
            main.ShowNotification($"Unexpected error: {e.Exception.Message}", NotificationLevel.Error);
        else
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}",
                "Elfive — Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

        e.Handled = true;
    }
}