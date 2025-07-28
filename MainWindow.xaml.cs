using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using inst.Enums;
using System.IO.Packaging;


namespace inst
{

    public partial class MainWindow : Window
    {
        private DatabaseConnection _dbConnection;
        private DatabaseConnection? _dbConnection2;
        private DatabaseManager _dbManager; // ERPORT

       //  private DatabaseManager _dbManager1; // Cílová Databáze načítá se z ConnectToTarget_Click
       //  private readonly string _gitPushFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update","auto.ps1");
       //  private readonly string _exportFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update","sql");
       // private readonly string _exportFolderPath_Bal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update 2","sql_Balikobot");

        private readonly string _solutionFolder = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string _gitPushFolder = GlobalConfig.Active.GitScriptPath;
        private readonly string _exportFolderPath = GlobalConfig.Active.ExportFolderPath;
        private readonly string sourceFolder;
        private readonly string outputFile;

        private List<string>? _SavedObjectNames = null;  // Načtené objekty ze souboru.


        public MainWindow(DatabaseConnection dbConnection)
        {
            InitializeComponent();
            
            outputFile = Path.Combine(_solutionFolder, "Script", "Script.txt");

            _dbConnection = dbConnection;
            bool isConnected = _dbConnection.CheckConnectionStatus();

            if (isConnected)
            {
                _dbManager = new DatabaseManager(_dbConnection);
                DatabaseSelector.ItemsSource = _dbManager.GetAllDatabases();
            }

            UpdateDatabaseStatus(_dbConnection, DbStatus);
            Console.WriteLine(_gitPushFolder, _exportFolderPath,GlobalConfig.Active);
        }


        /// <summary>
        /// Aktualizuje status cílové databáze podle stavu připojení
        /// 
        /// </summary>
        ///
        public void UpdateTargetStatus()
        {
            Dispatcher.Invoke(() =>
            {
                if (_dbConnection2 != null && _dbConnection2.SelectedDatabase != null)
                {
                    TargetStatus.Text = $"Connected to {_dbConnection2.SelectedDatabase.Name}";
                    TargetStatus.Foreground = Brushes.Green;
                }
                else
                {
                    TargetStatus.Text = "Disconnected";
                    TargetStatus.Foreground = Brushes.Red;
                }
            });
        }


        public void UpdateDatabaseStatus(DatabaseConnection dbConnection, TextBlock statusTextBlock)
        {
            Dispatcher.Invoke(() =>
            {
                if (dbConnection != null && dbConnection.SelectedDatabase != null)
                {
                    statusTextBlock.Text = $"Connected to {dbConnection.SelectedDatabase.Name}";
                    statusTextBlock.Foreground = Brushes.LightGreen;
                }
                else
                {
                    statusTextBlock.Text = "Disconnected";
                    statusTextBlock.Foreground = Brushes.Red;
                }
            });
        }



        private async Task LoadDatabaseObjectsAsync()
        {
            // var objectNames = FileHelper.LoadObjectNames(); //Načtu Seznam objektů ze souboru
            var count = 0;

            if (_SavedObjectNames == null)
            {
               // _SavedObjectNames = FileHelper.LoadObjectNames();
                _SavedObjectNames = _dbManager.GetObjectsFromTable(); // Načtu objekty z tabulky v databázi
            }

            if (_SavedObjectNames.Count == 0)
            {
                MessageBox.Show("No objects found in file!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await Task.Run(() =>
            {
                foreach (var objName in _SavedObjectNames)
                {
                    var totalObjectsCount = _SavedObjectNames.Count;
                    var obj = _dbManager.GetDatabaseObject(objName); //  Načtu objekt z databáze


                    //  Aktualizuj UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (obj != null)
                        {
                            ExportLog.Items.Add(new { FileName = obj.Name, Type = obj.Type, Status = "Found" });
                            count++;
                        }
                        else
                        {
                            ExportLog.Items.Add(new { FileName = objName, Type = "---", Status = "Not Found" });
                        }
                        Count.Content = count + "/" + totalObjectsCount;
                    });
                }
            });
        }

        private async void ExportSql_Click(object sender, RoutedEventArgs e)
        {
            ExportLog.Items.Clear();
            //var objectNames = FileHelper.LoadObjectNames(); // Načteme seznam objektů ze souboru
            if (_SavedObjectNames == null)
            {
                // _SavedObjectNames = FileHelper.LoadObjectNames();
                _SavedObjectNames = _dbManager.GetObjectsFromTable(); // Načtu objekty z tabulky v databázi
            }

            if (_SavedObjectNames.Count == 0)
            {
                MessageBox.Show("No objects found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadDatabaseObjectsAsync();


            Console.WriteLine("Začínám export...");
            await Task.Run(() =>
            {
                var sortedObjects = _dbManager.GetOrderedObjects(_SavedObjectNames); // podle závislostí
                _dbManager.ExportObjectsToFolder(_exportFolderPath, sortedObjects); //  Export 
                DeleteFromDirectory(sortedObjects);

            });

            

            Console.WriteLine("Export dokončen.");

        }

        public void DeleteFromDirectory(List<string> dtbObjects)
        {


        }


        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void MergeSql_Click(object sender, RoutedEventArgs e)
        {
            string solutionFolder = AppDomain.CurrentDomain.BaseDirectory;
          //  string sourceFolder = Path.Combine(solutionFolder, "Objects");
            string outputFile = Path.Combine(solutionFolder, "Script","Script.txt");

            EnsureDirectoryExists(_exportFolderPath);

            FileHelper.MergeSqlFiles(_exportFolderPath, outputFile);
        }

        private void ConnectToTarget_Click(object sender, RoutedEventArgs e)
        {
            
            //targetServerName = TargetDbServer.Text.Trim();
            //targetDatabaseName = TargetDbName.Text.Trim();

            //Console.WriteLine($"Connecting to {targetServerName} - {targetDatabaseName}");

            //if (string.IsNullOrEmpty(targetServerName) || string.IsNullOrEmpty(targetDatabaseName))
            //{
            //    MessageBox.Show(" Please enter both Server and Database name.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    return;
            //}


            //_dbConnection2 = new DatabaseConnection(targetServerName, targetDatabaseName, true);

            //if (_dbConnection2.Connect())
            //{
            //    _dbManager1 = new DatabaseManager(_dbConnection2);
            //    UpdateDatabaseStatus(_dbConnection2,TargetStatus);
            //}

        }


        protected override void OnClosed(EventArgs e)
        {
            _dbConnection?.Disconect();
            base.OnClosed(e);
           
        }

        private void openFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // zda složka existuje
                if (!Directory.Exists(_exportFolderPath))
                {
                    // Vytvoř pokud neexistuje
                    Directory.CreateDirectory(_exportFolderPath);
                  //  MessageBox.Show($"Složka {_exportFolderPath} byla vytvořena.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Otevři složku
                System.Diagnostics.Process.Start("explorer.exe", _exportFolderPath);
            }
            catch (Exception ex)
            {
                // zobraz chybu
                MessageBox.Show($"Nepodařilo se otevřít složku: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            _dbConnection.Disconect();

            this.Close();
            
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void OpenLoginWindow_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.ShowDialog();

           
        }

        private async void GenerateScript_Click(object sender, RoutedEventArgs e)
        {
            string idShop = ShopIdInput.Text;
            // Tady je celý SQL skript, který se vygeneruje, je potřeba to časem předělat.
            string sqlMapping = $@"
 DECLARE @IDShop INT = {idShop};

 BEGIN TRY
 BEGIN TRANSACTION
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Coal_shoptetProductMap')
 BEGIN
     INSERT INTO Coal_shoptetProductMap
     (
         id_externi_shop, name, description, productUrl, guid, adminUrl,
         visibility, Autor, DatPorizeni,BlokovaniEditoru,
         isbn, plu, mpn, shortDescription, availability, shopUpdated, 
         brandCode, code, metaDescription, packagingId, packagingAmount, 
         measureID, measureAmount, amountDecimalPlaces
     )
     VALUES
     (
         @IDSHop, 'tabkmenzbozi.nazev1', 'tabkmenzbozi.poznamka', 
         'tabkmenzbozi_ext._COAL_URL_Produktu', 'tabkmenzbozi_ext._COAL_idShop', 
         'tabkmenzbozi_ext._COAL_Origin_code', 'tabkmenzbozi_ext._COAL_Visibility', 
         SYSTEM_USER, GETDATE(),  
          NULL, 'tabkmenzbozi_ext._COAL_isbn', 
         'TabKmenZbozi.RegCis', 'TabKmenZbozi.RegCis', 
         'tabkmenzbozi_ext._COAL_price99', 'tabkmenzbozi_ext._COAL_dostupnostproCore', 
         'tabkmenzbozi_ext._COAL_datumzmenyproCore', 'tabkmenzbozi_ext._COAL_brandCode', 
         'tabkmenzbozi_ext._COAL_puvodniCODE', 'tabkmenzbozi_ext._COAL_metadescription', 
         'TabKmenZbozi_ext._COAL_packagingId', 'TabKmenZbozi_ext._COAL_packagingAmount', 
         'TabKmenZbozi_ext._COAL_measureID', 'TabKmenZbozi_ext._COAL_measureAmount', 
         'tabkmenzbozi_ext._COAL_amountDecimalPlaces'
     )
 END

 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_orderHeader_mapping')
 BEGIN
 INSERT INTO COAL_orderHeader_mapping
 (id_externi_shop,note,deliveryDate,Autor,DatPorizeni,BlokovaniEditoru,orderNumber,default_order,note3)
 VALUES(@IDSHop,'VerejnaPoznamka','DatumDodavky',SYSTEM_USER,GETDATE(),null,'ExterniCislo',1,'code3')
 END

 IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_orderHeader_mapping_ext')
 BEGIN
   INSERT INTO COAL_orderHeader_mapping_ext
     (
         deliveryTime, email, transportPrice, transportVat, variableSymbol,
         fullName, lastName, street, zip, city, phone, weight, Autor, DatPorizeni, 
         BlokovaniEditoru, id_externi_shop, houseNumber, deliveryName,
         deliveryStreet, deliveryStreetNo, deliveryCity, deliveryZip, deliveryCountry,
         deliveryCompany, idSubscription, orderURL, default_order, code3, PlaceOfCollection
     )
     VALUES
     (
         '_COAL_CasDoruceni', '_COAL_f_mail', '_COAL_CenaDopravy', '_COAL_transportVat',
         '_COAL_VariabilSymbol', '_COAL_f_jmenoprijmeni', '_COAL_f_lastName', '_COAL_f_ulice',
         '_COAL_f_psc', '_COAL_f_mesto', '_COAL_f_tel', '_COAL_weight', SYSTEM_USER, GETDATE(),
         NULL, @IDSHop, '_COAL_f_cp',
         '_COAL_d_jmenoprijmeni', '_COAL_d_ulice', '_COAL_d_cp', '_COAL_d_mesto', '_COAL_d_psc', '_COAL_d_zeme', '_COAL_d_firma', '_COAL_idSubscription','_COAL_adminurl', 0,'_code3',
         '_Balikobot_branch_id'
     )
 END

 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_createCompany_mapping_ext')
 BEGIN
 INSERT INTO COAL_createCompany_mapping_ext(Autor,DatPorizeni,BlokovaniEditoru,id_externi_shop,potential,priority,code,newsletter,reverseCharge,approved)
 VALUES(SYSTEM_USER,GETDATE(),null,@IDSHop,'_COAL_Neregistrovany', '_COAL_priority','_COAL_code', '_COAL_newsletter','_COAL_reverseCharge','_COAL_approved')
 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_createCompany_mapping')
 BEGIN
 INSERT INTO COAL_createCompany_mapping   (
          id_externi_shop, name, fullName, firstName, lastName, Autor, DatPorizeni, 
         BlokovaniEditoru, street, houseNumber, zip, city, status, note
     )
     VALUES
     (
          @IDShop, 'Nazev', 'DruhyNazev', 'Jmeno', 'Prijmeni', SYSTEM_USER, GETDATE(),
          NULL, 'Ulice', 'PopCislo', 
         'PSC', 'Misto', 'Stav', 'Poznamka'
     )
 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_createProduct_mapping')
 BEGIN
 INSERT INTO COAL_createProduct_mapping
     (
         id_externi_shop, Autor, DatPorizeni, BlokovaniEditoru, name, volume, 
         width, height, depth, weight
     )
     VALUES
     (
         @IDShop, SYSTEM_USER, GETDATE(), NULL, 'Nazev1', 
         'Objem', 'Sirka', 'Vyska', 'Hloubka', 'Hmotnost'
     )
 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_createProduct_mapping_ext')
 BEGIN
   INSERT INTO COAL_createProduct_mapping_ext
     (
         id_externi_shop, Autor, DatPorizeni, extID, price99, 
         seoKeyword, stock
     )
     VALUES
     (
         @IDShop, SYSTEM_USER, GETDATE(), '_COAL_extID', '_COAL_price99', 
         '_COAL_seoKeyword', '_COAL_stock'
     )
 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_orderProducts_mapping')
 BEGIN
  INSERT INTO COAL_orderProducts_mapping
    (
         id_externi_shop, name, note, productCode, Autor, DatPorizeni, 
         BlokovaniEditoru, default_order
     )
     VALUES
     (
         @IDShop, 'Nazev1', 'Poznamka', 'RegCis', SYSTEM_USER, GETDATE(), 
         NULL, 1
     )

 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_payOrder_mapping')
 BEGIN
  INSERT INTO COAL_payOrder_mapping
  (
         id_externi_shop, Autor, DatPorizeni, BlokovaniEditoru, ispaid, 
         variableSymbol, datePayment, default_order
     )
     VALUES
     (
         @IDShop, SYSTEM_USER, GETDATE(), NULL, '_COAL_zaplacena', 
         '_COAL_VariabilSymbol', '_COAL_datumZaplaceni', 1
     )
 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COAL_orderProducts_mapping_ext')
 BEGIN

  INSERT INTO COAL_orderProducts_mapping_ext
 (guid,Autor,DatPorizeni,BlokovaniEditoru,id_externi_shop,default_order)
 VALUES('_COAL_idshop',SYSTEM_USER,GETDATE(),null,@IDSHop,1)
 END
 --IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Coal_tabkmen_ext_mapping')
 BEGIN

  INSERT INTO Coal_tabkmen_ext_mapping
  (
         IDCoalshop, COAL_URL_produktu, COAL_idShop, COAL_origin_code, COAL_visibility,
         Autor, DatPorizeni, BlokovaniEditoru, COAL_puvodniCODE, nazev1
     )
     VALUES
     (
         @IDSHop, '_COAL_URL_produktu2', '_COAL_idShop2', '_COAL_origin_code2', 
         '_COAL_Visibility2', SYSTEM_USER, GETDATE(), NULL, '', 'nazev1'
     )
 END
  COMMIT TRANSACTION;

 END TRY

 BEGIN CATCH
     ROLLBACK TRANSACTION; -- Zrušení změn při chybě

     -- Vyhození chyby pro další zpracování
     DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
     DECLARE @ErrorSev INT = ERROR_SEVERITY();
     DECLARE @ErrorState INT = ERROR_STATE();
     RAISERROR (@ErrorMessage, @ErrorSev, @ErrorState);
                END CATCH;";   
            // Přiřazení textu do výstupního TextBoxu
            
            GeneratedScriptBox.Document.Blocks.Clear();
            GeneratedScriptBox.Document.Blocks.Add(new Paragraph(new Run(sqlMapping)));
            await HighlightSQL();

            Clipboard.SetText(sqlMapping); 
        }


        private Task HighlightSQL()
        {
           
            string sqlText = new TextRange(GeneratedScriptBox.Document.ContentStart, GeneratedScriptBox.Document.ContentEnd).Text;

          
            GeneratedScriptBox.Document.Blocks.Clear();

           
            Paragraph paragraph = new Paragraph();
            paragraph.Margin = new System.Windows.Thickness(0); 

            
            string keywords = @"\b(SELECT|INSERT|UPDATE|DELETE|FROM|WHERE|JOIN|INNER|LEFT|RIGHT|ON|AND|OR|NOT|NULL|AS|IN|BEGIN|END|TRANSACTION|ROLLBACK|COMMIT|TRY|CATCH|DECLARE|VALUES|SET|CASE|WHEN|THEN|ELSE)\b";
            string functions = @"\b(COUNT|SUM|AVG|MIN|MAX|LEN|GETDATE|NOW|DATEDIFF|CAST|CONVERT|ISNULL|COALESCE|ROUND|SUBSTRING|CHARINDEX|REPLACE|LTRIM|RTRIM|UPPER|LOWER)\b";
            string strings = @"'([^']*)'"; 
            string comments = @"(--.*?$)|(/\*[\s\S]*?\*/)"; 

           
            foreach (string line in sqlText.Split('\n'))
            {
                int index = 0;
                while (index < line.Length)
                {
                    Run run = new Run();
                    Match keywordMatch = Regex.Match(line.Substring(index), keywords, RegexOptions.IgnoreCase);
                    Match functionMatch = Regex.Match(line.Substring(index), functions, RegexOptions.IgnoreCase);
                    Match stringMatch = Regex.Match(line.Substring(index), strings);
                    Match commentMatch = Regex.Match(line.Substring(index), comments, RegexOptions.Multiline);

                    if (keywordMatch.Success && keywordMatch.Index == 0)
                    {
                        run.Text = keywordMatch.Value;
                        run.Foreground = Brushes.Blue;
                        index += keywordMatch.Length;
                    }
                    else if (functionMatch.Success && functionMatch.Index == 0)
                    {
                        run.Text = functionMatch.Value;
                        run.Foreground = Brushes.Purple;
                        index += functionMatch.Length;
                    }
                    else if (stringMatch.Success && stringMatch.Index == 0)
                    {
                        run.Text = stringMatch.Value;
                        run.Foreground = Brushes.Brown;
                        index += stringMatch.Length;
                    }
                    else if (commentMatch.Success && commentMatch.Index == 0)
                    {
                        run.Text = commentMatch.Value;
                        run.Foreground = Brushes.Green;
                        index += commentMatch.Length;
                    }
                    else
                    {
                        run.Text = line[index].ToString();
                        run.Foreground = Brushes.Black;
                        index++;
                    }

                    paragraph.Inlines.Add(run);
                }

                
                paragraph.Inlines.Add(new LineBreak());
            }

            FlowDocument doc = new FlowDocument(paragraph);
            doc.Blocks.Clear();
            doc.Blocks.Add(paragraph);
            GeneratedScriptBox.Document = doc;

            return Task.CompletedTask;
        }


        private void UpdateGit_Click(object sender, RoutedEventArgs e)
        {
           RunPowerShell(_gitPushFolder);
            //try
            //{
            //    var git = new GithubHelper();
            //    git.Run();
            //    MessageBox.Show("Změny byly odeslány na GitHub.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
            //}
            //catch (Exception ex)

            //{
            //    MessageBox.Show($"Chyba při odesílání na GitHub:\n{ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            //}

        }

       private void RunPowerShell( string path)
        {
            try
            {

                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{path}\"",
                    UseShellExecute = true, 
                    RedirectStandardOutput = false, 
                    RedirectStandardError = false,
                    CreateNoWindow = false 
                };

                using (Process process = new Process { StartInfo = processInfo })
                {
                    process.Start();

                    // Čtení výstupu
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();

                    process.WaitForExit();
                    

                    // Zobrazení výstupu
                    if (!string.IsNullOrEmpty(output))
                    {
                        Console.WriteLine(output); //"Changes --> GitHub"
                    }

                    // Zobrazení chyb
                    if (!string.IsNullOrEmpty(errors))
                    {
                        Console.WriteLine($"Chyby: {errors}");
                    }

                    // Kontrola exit kódu
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"PowerShell skript selhal s kódem {process.ExitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při spuštění PowerShell skriptu: {ex.Message}");
            }

        }

        private void DatabaseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DatabaseSelector.SelectedItem is string selectedDb)
            {
                _dbConnection.SelectDatabase(selectedDb);
                UpdateDatabaseStatus(_dbConnection, DbStatus);
            }
        }




    }


}
