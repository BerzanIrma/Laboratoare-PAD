using System.Windows.Input;

// Leaga un buton din XAML de o metoda din ViewModel.
// In XAML: Command="{Binding SendCommand}" -> la click, WPF apeleaza Execute(),
// care ruleaza actiunea primita in constructor.
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute; // actiunea de executat la click

    public RelayCommand(Action<object?> execute)
    {
        _execute = execute;
    }

    public bool CanExecute(object? parameter) => true; // butonul e mereu activ
    public void Execute(object? parameter) => _execute(parameter);

    // WPF foloseste acest eveniment ca sa reverifice CanExecute (ex: sa activeze/dezactiveze butonul)
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
