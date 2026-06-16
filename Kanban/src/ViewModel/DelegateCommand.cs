using System.Windows.Input;

namespace Kanban.ViewModel;

public class DelegateCommand<T> : ICommand
{
    private readonly Action<T>? _execute = null;
    private readonly Predicate<T>? _canExecute = null;

    public event EventHandler? CanExecuteChanged;

    #region Constructors
    public DelegateCommand(Action<T>? execute)
        : this(execute, null)
    {
    }

    public DelegateCommand(Action<T>? execute, Predicate<T>? canExecute)
    {
        if (execute == null)
            throw new ArgumentNullException("execute");

        _execute = execute;
        _canExecute = canExecute;
    }

    #endregion

    #region ICommand Members

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke((T)parameter) ?? true;
    }

    public void Execute(object? parameter) => _execute((T)parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    #endregion
}
