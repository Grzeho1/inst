using System;
using System.Threading.Tasks;
using System.Windows;

namespace inst
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            //InitializeComponent();
            //this.Loaded += async (s, e) => await StartLoading();
        }

        //private async Task StartLoading()
        //{
        //    UpdateStatus("Connecting to database...");
        //    await Task.Delay(3000);

        //    DatabaseConnection dbConnection = new DatabaseConnection();
        //    bool isConnected = dbConnection.Connect();

        //    if (isConnected)
        //    {
        //        UpdateStatus("Loading database objects...");
        //        await Task.Delay(3000);
        //    }
        //    else
        //    {
        //        MessageBox.Show("❌ Database connection failed!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }

        //    this.Hide();
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        MainWindow mainWindow = new MainWindow(dbConnection);
        //        mainWindow.Show();
        //    });

        //    this.Close();
        //}

        public void UpdateStatus(string message) => StatusText.Text = message;
    }
}
