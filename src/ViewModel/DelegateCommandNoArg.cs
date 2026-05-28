using System.Windows.Input;

namespace Kanban.ViewModel;

public class DelegateCommandNoArg : ICommand
{
    readonly Action? _execute;
    readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public DelegateCommandNoArg(Action? execute, Func<bool>? canExecute)
    {
        if (execute == null)
            throw new ArgumentNullException();
        _execute = execute;
        _canExecute = canExecute;
    }

    public DelegateCommandNoArg(Action execute)
    : this(execute, null)
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        if (_execute is not null) _execute();
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
