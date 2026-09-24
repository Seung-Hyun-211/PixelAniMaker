using System.Windows.Input;

namespace PixelAniMaker.App.Services;

/// <summary>Runs <paramref name="inner"/> only while <paramref name="allowed"/> is true (checked on every use).</summary>
public sealed class GuardedCommand(ICommand inner, Func<bool> allowed) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => inner.CanExecuteChanged += value;
        remove => inner.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter) => allowed() && inner.CanExecute(parameter);

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            inner.Execute(parameter);
    }
}
