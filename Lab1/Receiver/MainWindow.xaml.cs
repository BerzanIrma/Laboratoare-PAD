using System.Windows;
using System.Windows.Controls;

namespace Receiver
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void TopicSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string topic && DataContext is MainViewModel vm)
            {
                vm.NewTopicInput = topic;
            }
        }
    }
}