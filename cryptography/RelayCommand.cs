using System;
using System.Windows.Input;
namespace cryptography;
public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null): ICommand 
{
    private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}