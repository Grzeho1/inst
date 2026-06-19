using inst.Enums;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;


namespace inst
{

    public partial class MainWindow : Window
    {
        private DatabaseConnection _dbConnection;
        private DatabaseManager? _dbManager;

        private readonly string _gitPushFolder;
        private readonly string _exportFolderPath;

        private List<string>? _SavedObjectNames = null;
        private CancellationTokenSource? _cancellationTokenSource;
        


        public MainWindow(DatabaseConnection dbConnection)
        {
            InitializeComponent();

            _gitPushFolder = GlobalConfig.Active.GitScriptPath;
            _exportFolderPath = GlobalConfig.Active.ExportFolderPath;

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



        private async Task LoadDatabaseObjectsAsync(CancellationToken token = default)
        {
            // var objectNames = FileHelper.LoadObjectNames(); //Načtu Seznam objektů ze souboru
            var count = 0;

            if (_SavedObjectNames == null && _dbManager != null)
            {
                _SavedObjectNames = _dbManager.GetObjectsFromTable(token);
            }

            if (_SavedObjectNames == null || _SavedObjectNames.Count == 0)
            {
                MessageBox.Show("No objects found in file!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var savedObjects = _SavedObjectNames;
            await Task.Run(() =>
            {
                foreach (var objName in savedObjects)
                {
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine("přerušeno.");
                        return;
                    }

                    var totalObjectsCount = savedObjects.Count;
                    var obj = _dbManager?.GetDatabaseObject(objName, token);


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
            _cancellationTokenSource = new CancellationTokenSource();

            ExportLog.Items.Clear();

            if (!Directory.Exists(_exportFolderPath))
            {
                Directory.CreateDirectory(_exportFolderPath);
            }

            if (_dbManager == null)
            {
                MessageBox.Show("Database manager not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_SavedObjectNames == null)
            {
                _SavedObjectNames = _dbManager.GetObjectsFromTable(_cancellationTokenSource.Token);
            }

            if (_SavedObjectNames.Count == 0)
            {
                MessageBox.Show("No objects found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadDatabaseObjectsAsync(_cancellationTokenSource.Token);

            Console.WriteLine("Začínám export...");
            var objectsToExport = _SavedObjectNames;
            await Task.Run(() =>
            {
                var sortedObjects = _dbManager.GetOrderedObjects(objectsToExport, _cancellationTokenSource.Token);
                _dbManager.ExportObjectsToFolder(_exportFolderPath, sortedObjects, _cancellationTokenSource.Token);
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
            _cancellationTokenSource?.Cancel(); // Zruší běžící úlohu, pokud je.


            _dbConnection?.DisconnectAsync();
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
            string templateShopIdText = TemplateShopIdInput.Text.Trim();
            string newShopId = ShopIdInput.Text.Trim();

            if (!int.TryParse(templateShopIdText, out int templateShopId))
            {
                MessageBox.Show("Zadej platné Template ShopID.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(newShopId))
            {
                MessageBox.Show("Zadej nové Shop ID.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dbManager == null)
            {
                MessageBox.Show("Database manager not initialized.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int defaultOrder = RadioEP.IsChecked == true ? 0 : 1;

            var mappingData = await Task.Run(() => _dbManager.GetMappingValues(templateShopId, defaultOrder));
            string sqlMapping = BuildSqlScript(newShopId, mappingData);

            GeneratedScriptBox.Document.Blocks.Clear();
            GeneratedScriptBox.Document.Blocks.Add(new Paragraph(new Run(sqlMapping)));
            await HighlightSQL();

            Clipboard.SetText(sqlMapping);
        }

        private static string BuildInsert(string tableName, string shopIdCol, string newShopId, System.Data.DataRow row)
        {
            var skipCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "ID", "Autor", "DatPorizeni", "BlokovaniEditoru", "DatZmeny", "DatVytvoreni", "Zmenil" };

            var cols = new List<string>();
            var vals = new List<string>();

            foreach (System.Data.DataColumn col in row.Table.Columns)
            {
                if (skipCols.Contains(col.ColumnName)) continue;

                cols.Add(col.ColumnName);

                if (col.ColumnName.Equals(shopIdCol, StringComparison.OrdinalIgnoreCase))
                {
                    vals.Add("@IDShop");
                }
                else
                {
                    var raw = row[col];
                    if (raw == null || raw == DBNull.Value)
                    {
                        vals.Add("NULL");
                    }
                    else if (col.DataType == typeof(int) || col.DataType == typeof(long) ||
                             col.DataType == typeof(short) || col.DataType == typeof(decimal) ||
                             col.DataType == typeof(double) || col.DataType == typeof(float))
                    {
                        vals.Add(raw.ToString()!);
                    }
                    else
                    {
                        vals.Add($"'{raw.ToString()!.Replace("'", "''")}'");
                    }
                }
            }

            return $"INSERT INTO {tableName}\n    ({string.Join(", ", cols)})\nVALUES\n    ({string.Join(", ", vals)})";
        }

        private static string BuildSqlScript(string newShopId, Dictionary<string, List<System.Data.DataRow>> data)
        {
            var tabkmenShopCol = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Coal_tabkmen_ext_mapping" };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"DECLARE @IDShop INT = {newShopId};");
            sb.AppendLine();
            sb.AppendLine("BEGIN TRY");
            sb.AppendLine("BEGIN TRANSACTION");

            foreach (var (table, rows) in data)
            {
                if (rows.Count == 0) continue;

                string shopIdCol = tabkmenShopCol.Contains(table) ? "IDCoalshop" : "id_externi_shop";

                sb.AppendLine();
                sb.AppendLine($"-- {table}");
                sb.AppendLine("BEGIN");
                foreach (var row in rows)
                    sb.AppendLine(BuildInsert(table, shopIdCol, newShopId, row));
                sb.AppendLine("END");
            }

            sb.AppendLine();
            sb.AppendLine("COMMIT TRANSACTION;");
            sb.AppendLine();
            sb.AppendLine("END TRY");
            sb.AppendLine("BEGIN CATCH");
            sb.AppendLine("    ROLLBACK TRANSACTION;");
            sb.AppendLine("    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();");
            sb.AppendLine("    DECLARE @ErrorSev INT = ERROR_SEVERITY();");
            sb.AppendLine("    DECLARE @ErrorState INT = ERROR_STATE();");
            sb.AppendLine("    RAISERROR (@ErrorMessage, @ErrorSev, @ErrorState);");
            sb.AppendLine("END CATCH;");

            return sb.ToString();
        }

        private Task HighlightSQL()
        {
            string sqlText = new TextRange(GeneratedScriptBox.Document.ContentStart, GeneratedScriptBox.Document.ContentEnd).Text;

            // Vypočítej všechny barevné spany najednou na celém textu
            var spans = new List<(int Start, int End, Brush Color)>();
            foreach (Match m in SqlKeywords.Matches(sqlText))
                spans.Add((m.Index, m.Index + m.Length, Brushes.Blue));
            foreach (Match m in SqlFunctions.Matches(sqlText))
                spans.Add((m.Index, m.Index + m.Length, Brushes.Purple));
            foreach (Match m in SqlStrings.Matches(sqlText))
                spans.Add((m.Index, m.Index + m.Length, Brushes.Brown));
            foreach (Match m in SqlComments.Matches(sqlText))
                spans.Add((m.Index, m.Index + m.Length, Brushes.Green));

            // Seřaď a odstraň překryvy (první match vyhrává)
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            var filtered = new List<(int Start, int End, Brush Color)>();
            int lastEnd = 0;
            foreach (var s in spans)
            {
                if (s.Start >= lastEnd) { filtered.Add(s); lastEnd = s.End; }
            }

            // Sestav paragraph — minimální počet Run elementů
            var paragraph = new Paragraph { Margin = new System.Windows.Thickness(0) };

            void AddText(string text, Brush brush)
            {
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string part = lines[i].TrimEnd('\r');
                    if (part.Length > 0)
                        paragraph.Inlines.Add(new Run(part) { Foreground = brush });
                    if (i < lines.Length - 1)
                        paragraph.Inlines.Add(new LineBreak());
                }
            }

            int pos = 0;
            foreach (var (start, end, color) in filtered)
            {
                if (start > pos) AddText(sqlText[pos..start], Brushes.Black);
                AddText(sqlText[start..end], color);
                pos = end;
            }
            if (pos < sqlText.Length) AddText(sqlText[pos..], Brushes.Black);

            var doc = new FlowDocument(paragraph);
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




        private static readonly Regex SqlKeywords  = new(@"\b(SELECT|INSERT|UPDATE|DELETE|FROM|WHERE|JOIN|INNER|LEFT|RIGHT|ON|AND|OR|NOT|NULL|AS|IN|BEGIN|END|TRANSACTION|ROLLBACK|COMMIT|TRY|CATCH|DECLARE|VALUES|SET|CASE|WHEN|THEN|ELSE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SqlFunctions = new(@"\b(COUNT|SUM|AVG|MIN|MAX|LEN|GETDATE|NOW|DATEDIFF|CAST|CONVERT|ISNULL|COALESCE|ROUND|SUBSTRING|CHARINDEX|REPLACE|LTRIM|RTRIM|UPPER|LOWER)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SqlStrings   = new(@"'[^']*'", RegexOptions.Compiled);
        private static readonly Regex SqlComments  = new(@"(--.*?$)|(/\*[\s\S]*?\*/)", RegexOptions.Multiline | RegexOptions.Compiled);
    }


}
