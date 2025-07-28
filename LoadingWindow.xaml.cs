using System;
using System.Threading.Tasks;
using System.Windows;
using inst.Enums;
namespace inst
{
    public partial class LoadingWindow : Window
    {
        private DatabaseConnection _dbConnection = null!; // Initialize with null-forgiving operator to satisfy the compiler.  

        public LoadingWindow()
        {
            InitializeComponent();
            InitializeDatabaseConnection();
        }

        public async void InitializeDatabaseConnection()
        {
            //if (GlobalConfig.SelectedKonektor == KonektorEnums.Konektor.Univerzal)
            //{
            //    _dbConnection = new DatabaseConnection(GlobalConfig.ServerIP, GlobalConfig.Active.Database, true);
            //}
            //else
            //{
            //    _dbConnection = new DatabaseConnection(GlobalConfig.ServerIP, GlobalConfig.Active.Database, true);
            //}
            _dbConnection = new DatabaseConnection(GlobalConfig.ServerIP, GlobalConfig.Active.Database, true);

            UpdateStatus($"Connecting to server ");

            bool isConnected = await Task.Run(() => _dbConnection.Connect());
            if (isConnected)
            {
                UpdateStatus("Connected to server, loading database");

                MainWindow mainWindow = new MainWindow(_dbConnection);
                mainWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Chyba při připojení k databázi", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        public void UpdateStatus(string message) => StatusText.Text = message;
    }
}
