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
using ZebraPrintUtility.ViewModels;

namespace ZebraPrintUtility;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;

        // Auto-save settings when window closes
        this.Closing += (s, e) => viewModel.SaveSettings();
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag != null)
        {
            if (int.TryParse(rb.Tag.ToString(), out int index))
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.SelectedTabIndex = index;
                }
            }
        }
    }

    private void ClearZpl_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ZplContent = string.Empty;
        }
    }
}