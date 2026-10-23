
using System;
using System.Windows;
using LotTrackingWpf.Data;
using LotTrackingWpf.Services;

namespace LotTrackingWpf;

public partial class App : Application
{
    private async void App_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            var db = new Database();

            await db.InitializeAsync();

            var auth = new AuthService(db);

            while (true)
            {
                var login = new LoginWindow(auth);

                if (login.ShowDialog() != true)
                {
                    break;
                }

                var main = new MainWindow(
                    db,
                    login.AuthenticatedUser!
                );

                MainWindow = main;

                main.ShowDialog();

                if (!main.LogoutRequested)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Application startup failed. " +
                "Check MySQL and appsettings.json.\n\n" +
                ex.Message,
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        Shutdown();
    }
}
