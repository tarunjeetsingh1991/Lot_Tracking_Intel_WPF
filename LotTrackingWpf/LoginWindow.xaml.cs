
using System;
using System.Windows;
using LotTrackingWpf.Models;
using LotTrackingWpf.Services;
using LotTrackingWpf.ViewModels;

namespace LotTrackingWpf;

public partial class LoginWindow : Window
{
    private readonly AuthService auth;

    private readonly LoginViewModel vm = new();

    public UserAccount? AuthenticatedUser { get; private set; }

    public LoginWindow(AuthService auth)
    {
        InitializeComponent();

        this.auth = auth;

        DataContext = vm;

        Loaded += InitializeLogin;
    }

    private async void InitializeLogin(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            vm.FirstRun = !await auth.HasUsersAsync();

            SubmitButton.Content = vm.FirstRun
                ? "Create account"
                : "Sign in";
        }
        catch (Exception ex)
        {
            vm.Error = "Unable to read users: " + ex.Message;

            SubmitButton.IsEnabled = false;
        }
    }

    private async void Submit_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (vm.Busy)
        {
            return;
        }

        vm.Busy = true;
        SubmitButton.IsEnabled = false;
        vm.Error = "";

        try
        {
            var password = PasswordInput.Password;

            // First-time administrator registration
            if (vm.FirstRun)
            {
                await auth.RegisterInitialAdminAsync(
                    vm.Username,
                    password
                );

                vm.FirstRun = false;
            }

            // Authenticate user
            var user = await auth.LoginAsync(
                vm.Username,
                password
            );

            if (user == null)
            {
                vm.Error = "Invalid username or password.";
                return;
            }

            AuthenticatedUser = user;

            PasswordInput.Clear();

            DialogResult = true;
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
        finally
        {
            vm.Busy = false;

            SubmitButton.IsEnabled = true;

            SubmitButton.Content = vm.FirstRun
                ? "Create account"
                : "Sign in";
        }
    }
}

// user name : tarunjeet91
// password : Admin@1234
