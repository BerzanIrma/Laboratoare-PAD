using System.Windows;

namespace Sender
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent(); // construieste controalele descrise in MainWindow.xaml

            // DataContext = obiectul de care se leaga toate {Binding ...} din XAML
            DataContext = new MainViewModel();
        }
    }
}