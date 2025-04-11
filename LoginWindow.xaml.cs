using Microsoft.SqlServer.Management.Smo.Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace inst
{
    /// <summary>
    /// Interakční logika pro LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {

        //private DatabaseConnection _dbConnection2;
        //private DatabaseManager _dbManager2;
        //string targetServerName;
        //string targetDatabaseName;


        //public LoginWindow()
        //{
           
        //}


        //private void LoginButton_Click(object sender, RoutedEventArgs e)
        //{
        //    targetServerName = IpTextbox.Text.Trim();
        //    targetDatabaseName = DatabaseTextbox.Text.Trim();

        //    Console.WriteLine($"Connecting to {targetServerName} - {targetDatabaseName}");

        //    if (string.IsNullOrEmpty(targetServerName) || string.IsNullOrEmpty(targetDatabaseName))
        //    {
        //        MessageBox.Show(" Please enter both Server and Database name.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        //        return;
        //    }

        //    _dbConnection2 = new DatabaseConnection(targetServerName, targetDatabaseName, true);

        //    if (_dbConnection2.Connect())
        //    {
        //        _dbManager2 = new DatabaseManager(_dbConnection2);
        //        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
        //        mainWindow.UpdateDatabaseStatus(_dbConnection2,mainWindow.TargetStatus);
        //        this.Close();
        //    }
        //}
    }
}
