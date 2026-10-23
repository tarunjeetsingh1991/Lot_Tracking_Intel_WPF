
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LotTrackingWpf.Infrastructure;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> action;
    private bool busy;

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<Task> action)
    {
        this.action = action;
    }

    public bool CanExecute(object? parameter)
    {
        return !busy;
    }

    public async void Execute(object? parameter)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Operation failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            busy = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
