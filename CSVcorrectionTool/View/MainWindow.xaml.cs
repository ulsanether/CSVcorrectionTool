using System.Windows;

using CSVcorrectionTool.Services;
using CSVcorrectionTool.View;
using CSVcorrectionTool.ViewModels;


namespace CSVcorrectionTool
{
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var csvService = new CSVService();
            var viewModel = new MainViewModel(csvService);

            DataContext = viewModel;

        }
    }
}