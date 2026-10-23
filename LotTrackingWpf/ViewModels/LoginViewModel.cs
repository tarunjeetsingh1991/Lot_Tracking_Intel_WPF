
using CommunityToolkit.Mvvm.ComponentModel;

namespace LotTrackingWpf.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string username = "";

    [ObservableProperty]
    private string error = "";

    [ObservableProperty]
    private bool firstRun;

    [ObservableProperty]
    private bool busy;

    public string Heading =>
        FirstRun
            ? "Create administrator account"
            : "Sign in to Lot Tracking";

    partial void OnFirstRunChanged(bool value)
    {
        OnPropertyChanged(nameof(Heading));
    }
}
