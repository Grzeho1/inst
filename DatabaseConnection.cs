using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Windows;

namespace inst
{
    public class DatabaseConnection
    {
        public Server ServerInstance { get; private set; }
        public Database SelectedDatabase { get; private set; }

        //private readonly string serverName = @"172.16.131.81"; // @"DESKTOP-FUQ15OI\SQLEXPRESS";
        //private readonly string databaseName = "Helios003"; // "testovaci";
        //private readonly bool useWindowsAuth = true; //  False pokud je SQL Authentication
        //private readonly string userId = "tomas"; //  pokud je `useWindowsAuth = false`
        //private readonly string password = "123456"; // pokud je `useWindowsAuth = false`

        private readonly string _serverName;
        private readonly string _databaseName;
        private readonly bool _useWindowsAuth;
        private readonly string _userId;
        private readonly string _password;

        public DatabaseConnection(string serverName, string databaseName, bool useWindowsAuth = true, string userId = "", string password = "")
        {
            _serverName = serverName;
            _databaseName = databaseName;
            _useWindowsAuth = useWindowsAuth;
            _userId = userId;
            _password = password;
        }

        /// <summary>
        /// Připojí se ke specifikovanému SQL serveru a vybere specifikovanou databázi.
        /// </summary>
        /// <returns>True, pokud je připojení a výběr databáze úspěšný, jinak false.</returns>
  
        public bool Connect()
        {
            try
            {
                ServerConnection connection;

                if (_useWindowsAuth)
                {
                    connection = new ServerConnection(_serverName)
                    {
                        LoginSecure = true // Windows Authentication
                    };
                }
                else
                {
                    connection = new ServerConnection(_serverName, _userId, _password)
                    {
                        LoginSecure = false // SQL Authentication
                    };
                }

                ServerInstance = new Server(connection);
                ServerInstance.ConnectionContext.Connect(); //  Ruční připojení k serveru pro ověření

                //  Kontrola existence databáze
                if (ServerInstance.Databases.Contains(_databaseName))
                {
                    SelectedDatabase = ServerInstance.Databases[_databaseName];
                    Console.WriteLine($" Connected to database: {_databaseName}");
                    return true;
                }
                else
                {
                    MessageBox.Show($"Database '{_databaseName}' not found on server '{_serverName}'.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($" Database connection failed!\nError: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Database connection failed: {ex.Message}");
                return false;
            }
        }


        public void Disconect()
        {
            if (ServerInstance != null && ServerInstance.ConnectionContext.IsOpen)
            {
                ServerInstance.ConnectionContext.Disconnect();
                Console.WriteLine("Disconnected from server.");
            }
        }
    }
}
