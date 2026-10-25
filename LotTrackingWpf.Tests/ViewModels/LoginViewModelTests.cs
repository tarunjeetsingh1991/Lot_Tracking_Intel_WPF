
using System.Collections.Generic;
using System.ComponentModel;
using LotTrackingWpf.ViewModels;
using Xunit;

namespace LotTrackingWpf.Tests.ViewModels;

public class LoginViewModelTests
{
    // TEST 1
    [Fact]
    public void Constructor_DefaultValues_AreCorrect()
    {
        var vm = new LoginViewModel();

        Assert.Equal("", vm.Username);
        Assert.Equal("", vm.Error);
        Assert.False(vm.FirstRun);
        Assert.False(vm.Busy);
    }

    // TEST 2
    [Fact]
    public void Heading_DefaultMode_ShowsSignIn()
    {
        var vm = new LoginViewModel();

        Assert.Equal(
            "Sign in to Lot Tracking",
            vm.Heading);
    }

    // TEST 3
    [Fact]
    public void Heading_FirstRunMode_ShowsCreateAdmin()
    {
        var vm = new LoginViewModel
        {
            FirstRun = true
        };

        Assert.Equal(
            "Create administrator account",
            vm.Heading);
    }

    // TEST 4
    [Fact]
    public void FirstRun_ChangedToTrue_UpdatesHeading()
    {
        var vm = new LoginViewModel();

        vm.FirstRun = true;

        Assert.Equal(
            "Create administrator account",
            vm.Heading);
    }

    // TEST 5
    [Fact]
    public void FirstRun_ChangedBackToFalse_UpdatesHeading()
    {
        var vm = new LoginViewModel
        {
            FirstRun = true
        };

        vm.FirstRun = false;

        Assert.Equal(
            "Sign in to Lot Tracking",
            vm.Heading);
    }

    // TEST 6
    [Fact]
    public void Username_SetValue_UpdatesProperty()
    {
        var vm = new LoginViewModel();

        vm.Username = "admin";

        Assert.Equal("admin", vm.Username);
    }

    // TEST 7
    [Fact]
    public void Error_SetValue_UpdatesProperty()
    {
        var vm = new LoginViewModel();

        vm.Error = "Invalid username or password.";

        Assert.Equal(
            "Invalid username or password.",
            vm.Error);
    }

    // TEST 8
    [Fact]
    public void Busy_SetTrue_UpdatesProperty()
    {
        var vm = new LoginViewModel();

        vm.Busy = true;

        Assert.True(vm.Busy);
    }

    // TEST 9
    [Fact]
    public void Busy_SetFalse_UpdatesProperty()
    {
        var vm = new LoginViewModel
        {
            Busy = true
        };

        vm.Busy = false;

        Assert.False(vm.Busy);
    }

    // TEST 10
    [Fact]
    public void Username_Changed_RaisesPropertyChanged()
    {
        var vm = new LoginViewModel();

        string? changedProperty = null;

        vm.PropertyChanged += (sender, args) =>
        {
            changedProperty = args.PropertyName;
        };

        vm.Username = "testuser";

        Assert.Equal(
            nameof(LoginViewModel.Username),
            changedProperty);
    }

    // TEST 11
    [Fact]
    public void Error_Changed_RaisesPropertyChanged()
    {
        var vm = new LoginViewModel();

        string? changedProperty = null;

        vm.PropertyChanged += (sender, args) =>
        {
            changedProperty = args.PropertyName;
        };

        vm.Error = "Login failed";

        Assert.Equal(
            nameof(LoginViewModel.Error),
            changedProperty);
    }

    // TEST 12
    [Fact]
    public void Busy_Changed_RaisesPropertyChanged()
    {
        var vm = new LoginViewModel();

        string? changedProperty = null;

        vm.PropertyChanged += (sender, args) =>
        {
            changedProperty = args.PropertyName;
        };

        vm.Busy = true;

        Assert.Equal(
            nameof(LoginViewModel.Busy),
            changedProperty);
    }

    // TEST 13
    [Fact]
    public void FirstRun_Changed_RaisesHeadingNotification()
    {
        var vm = new LoginViewModel();

        var changedProperties = new List<string>();

        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        vm.FirstRun = true;

        Assert.Contains(
            nameof(LoginViewModel.FirstRun),
            changedProperties);

        Assert.Contains(
            nameof(LoginViewModel.Heading),
            changedProperties);
    }

    // TEST 14
    [Fact]
    public void FirstRun_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = new LoginViewModel();

        int notificationCount = 0;

        vm.PropertyChanged += (sender, args) =>
        {
            notificationCount++;
        };

        vm.FirstRun = false;

        Assert.Equal(0, notificationCount);
    }

    // TEST 15
    [Fact]
    public void Username_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = new LoginViewModel
        {
            Username = "admin"
        };

        int notificationCount = 0;

        vm.PropertyChanged += (sender, args) =>
        {
            notificationCount++;
        };

        vm.Username = "admin";

        Assert.Equal(0, notificationCount);
    }
}
