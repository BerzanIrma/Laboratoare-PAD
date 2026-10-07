using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SenderGUI;

/// <summary>
/// Codul din spatele ferestrei MainWindow.xaml.
/// Singurul lui rol: leaga fereastra de MainViewModel.
/// Toata logica este in MainViewModel.cs, nu aici (MVVM).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        // construieste controalele descrise in MainWindow.xaml
        InitializeComponent();

        // DataContext = de unde iau valorile toate {Binding ...} din XAML.
        // De ex. {Binding Topic} citeste/scrie MainViewModel.Topic.
        DataContext = new MainViewModel();
    }
}
