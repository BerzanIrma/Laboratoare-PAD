using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SenderGUI;

// Clasa de baza pentru ViewModel-uri.
// Implementeaza INotifyPropertyChanged: evenimentul prin care fereastra afla ca o valoare s-a schimbat.
// Fara el, daca scrii in cod Status = "...", textul din fereastra NU s-ar actualiza.
public abstract class ViewModelBase : INotifyPropertyChanged
{
    // fereastra (prin {Binding}) se aboneaza automat la acest eveniment
    public event PropertyChangedEventHandler? PropertyChanged;

    // Folosit in setter-ul fiecarei proprietati: set => SetProperty(ref _camp, value);
    //  1. daca valoarea e aceeasi, nu face nimic
    //  2. altfel o salveaza in camp
    //  3. si anunta fereastra ce proprietate s-a schimbat
    //     ([CallerMemberName] completeaza automat numele proprietatii, ex: "Status")
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
