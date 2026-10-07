using System.ComponentModel;
using System.Runtime.CompilerServices;

// Clasa de baza pentru ViewModel-uri. Implementeaza INotifyPropertyChanged:
// cand o proprietate se schimba, anunta UI-ul, iar {Binding} din XAML se actualizeaza singur.
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // Folosit in setter-ele proprietatilor: seteaza valoarea si anunta UI-ul.
    // [CallerMemberName] completeaza automat numele proprietatii care a apelat (ex: "Status").
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false; // aceeasi valoare -> nu anuntam nimic
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
