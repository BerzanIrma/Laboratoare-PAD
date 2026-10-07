using System.Windows.Input;

namespace SenderGUI;

// Leaga un buton din XAML de o metoda din ViewModel.
// In XAML: Command="{Binding SendCommand}" -> la click, WPF apeleaza Execute -> se ruleaza metoda primita in constructor.
// (Butoanele WPF nu pot apela direct o metoda; au nevoie de un obiect ICommand.)
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;   // metoda care se ruleaza la click

    public RelayCommand(Action<object?> execute)
    {
        _execute = execute;
    }

    // butonul e mereu activ
    public bool CanExecute(object? parameter) => true;

    // apelat de WPF la click / Enter
    public void Execute(object? parameter) => _execute(parameter);

    // WPF intreaba din cand in cand din nou CanExecute (aici nu conteaza, e mereu true)
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
