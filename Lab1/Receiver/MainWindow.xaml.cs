using System.Windows;
using System.Windows.Controls;

namespace Receiver
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent(); // construieste controalele descrise in MainWindow.xaml

            // DataContext = obiectul de care se leaga toate {Binding ...} din XAML
            DataContext = new MainViewModel();
        }

        // click pe un buton din lista de topicuri disponibile -> completeaza casuta de abonare
        private void TopicSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string topic && DataContext is MainViewModel vm)
            {
                vm.NewTopicInput = topic;
            }
        }
    }
}