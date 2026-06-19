using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Shapes;

namespace inst
{
    public record CoalshopItem(int Id, string Nazev);

    /// <summary>
    /// Manages database operations such as retrieving and exporting database objects.
    /// </summary>
    public class DatabaseManager
    {
        private readonly DatabaseConnection _dbConnection;
        private readonly Database _database;
        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseManager"/> class.
        /// </summary>
        /// <param name="dbConnection">The database connection to be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when the dbConnection is null.</exception>
        /// <exception cref="Exception">Thrown when the ServerInstance or SelectedDatabase is not initialized.</exception>
        public DatabaseManager(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));

            if (_dbConnection.ServerInstance == null || _dbConnection.SelectedDatabase == null)
            {
                throw new Exception("DatabaseConnection is not initialized.");
            }
            _database = _dbConnection.SelectedDatabase;


        }

        /// <summary>
        /// Získá všechny objekty v databázi (uložené procedury, triggery, pohledy).
        /// </summary>
        /// <returns>Seznam všech objektů v databázi.</returns>
        public List<DatabaseObject> GetAllObjects()
        {
            List<DatabaseObject> objects = new List<DatabaseObject>();

            foreach (StoredProcedure sp in _database.StoredProcedures)
            {
                if (!sp.IsSystemObject)
                    objects.Add(new DatabaseObject(sp.Name, "Stored Procedure"));
            }

            foreach (Table table in _database.Tables)
            {
                foreach (Trigger trigger in table.Triggers)
                    objects.Add(new DatabaseObject(trigger.Name, "Trigger"));
            }

            foreach (View view in _database.Views)
            {
                if (!view.IsSystemObject)
                    objects.Add(new DatabaseObject(view.Name, "View"));
            }

            foreach (UserDefinedFunction fn in _database.UserDefinedFunctions)
            {
                if (!fn.IsSystemObject)
                    objects.Add(new DatabaseObject(fn.Name, "Function"));
            }

            return objects;
        }

        private static readonly Regex CreateToAlterRegex = new Regex(
            @"\bCREATE\s+(PROCEDURE|PROC|VIEW|FUNCTION|TRIGGER)\b",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Exportuje vybrané objekty jako ALTER skripty do zadané složky.
        /// Pořadí dle vstupního seznamu (žádné topologické řazení — ALTER objekty už v cílové DB existují).
        /// </summary>
        public void ExportObjectsAsAlter(string exportFolderPath, List<string> objectNames, CancellationToken token)
        {
            if (!Directory.Exists(exportFolderPath))
            {
                Directory.CreateDirectory(exportFolderPath);
            }

            foreach (var existingFile in Directory.GetFiles(exportFolderPath, "*.sql", SearchOption.TopDirectoryOnly))
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("přerušeno.");
                    return;
                }

                File.Delete(existingFile);
                Console.WriteLine($" Deleted old update export: {existingFile}");
            }

            HashSet<string> usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int order = 1;

            foreach (var objName in objectNames)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("přerušeno.");
                    return;
                }

                string? sqlText = GetObjectText(objName);

                if (string.IsNullOrEmpty(sqlText))
                {
                    Console.WriteLine($" Object '{objName}' not found.");
                    continue;
                }

                string altered = CreateToAlterRegex.Replace(
                    sqlText,
                    m => "ALTER " + m.Groups[1].Value,
                    1);

                string fileName;
                do
                {
                    fileName = $"{order}_{objName}.sql";
                    order++;
                } while (usedFileNames.Contains(fileName));

                string filePath = System.IO.Path.Combine(exportFolderPath, fileName);
                File.WriteAllText(filePath, altered);
                Console.WriteLine($" Exported (ALTER): {filePath}");

                usedFileNames.Add(fileName);
            }
        }

        /// <summary>
        /// Exportuje specifikované objekty databáze do zadané složky.
        /// </summary>
        /// <param name="exportFolderPath">Cesta ke složce, kam budou objekty exportovány.</param>
        /// <param name="objectNames">Seznam názvů objektů, které mají být exportovány.</param>
        public void ExportObjectsToFolder(string exportFolderPath, List<string> objectNames, CancellationToken token)
        {
            if (!Directory.Exists(exportFolderPath))
            {
                Directory.CreateDirectory(exportFolderPath);
            }

            foreach (var existingFile in Directory.GetFiles(exportFolderPath, "*.sql", SearchOption.TopDirectoryOnly))
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("přerušeno.");
                    return;
                }

                File.Delete(existingFile);
                Console.WriteLine($" Deleted old export: {existingFile}");
            }

            var sortedObjects = GetOrderedObjects(objectNames, token).Distinct().ToList();
            HashSet<string> usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int order = 1;
            foreach (var objName in sortedObjects)
            {
                string? sqlText = GetObjectText(objName);

                if (!string.IsNullOrEmpty(sqlText))
                {
                    string fileName;
                    do
                    {
                        fileName = $"{order}_{objName}.sql";
                        order++;
                    } while (usedFileNames.Contains(fileName));

                    string filePath = System.IO.Path.Combine(exportFolderPath, fileName);
                    File.WriteAllText(filePath, sqlText);
                    Console.WriteLine($" Exported: {filePath}");

                    usedFileNames.Add(fileName);
                }
                else
                {
                    Console.WriteLine($" Object '{objName}' not found.");
                }
            }
        }


        /// <summary>
        /// Získá SQL text specifikovaného objektu databáze.
        /// </summary>
        /// <param name="objectName">Název objektu databáze.</param>
        /// <returns>SQL text objektu.</returns>
        private string? GetObjectText(string objectName)
        {
            string query = $@"
        SELECT
            definition,
            CASE WHEN uses_quoted_identifier = 1 THEN 'SET QUOTED_IDENTIFIER ON;' ELSE 'SET QUOTED_IDENTIFIER OFF;' END AS quoted_identifier_setting
        FROM sys.sql_modules
        WHERE object_id = OBJECT_ID('{objectName}');
    ";

            var dataset = _database.ExecuteWithResults(query);

            //  dataset a tabulka i řádky musí existovat
            if (dataset == null || dataset.Tables.Count == 0 || dataset.Tables[0].Rows.Count == 0)
                return null;

            var row = dataset.Tables[0].Rows[0];

            string? definition = row["definition"]?.ToString();
            string? quotedIdentifierSetting = row["quoted_identifier_setting"]?.ToString();


            if (string.IsNullOrWhiteSpace(definition))
                return null;

            quotedIdentifierSetting ??= "SET QUOTED_IDENTIFIER ON;";

            return $"{quotedIdentifierSetting}\nGO\n{definition}";
        }


        /// <summary>
        /// Získá specifikovaný objekt databáze podle jeho názvu.
        /// </summary>
        /// <param name="objectName">Název objektu databáze, který má být získán.</param>
        /// <returns>Objekt databáze, pokud je nalezen; jinak null.</returns>
        public DatabaseObject? GetDatabaseObject(string objectName, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("přerušeno.");
                return null;
            }

            //  Hledám mezi procedurami
            if (_database.StoredProcedures.Contains(objectName) && !_database.StoredProcedures[objectName].IsSystemObject)
            {
                return new DatabaseObject(objectName, "Stored Procedure", "FOUND");
            }

            //  Hledám mezi triggery
            string triggerQuery = $@"
                    SELECT name
                    FROM sys.triggers
                    WHERE name = '{objectName}';
                    ";

            var triggerDataset = _database.ExecuteWithResults(triggerQuery);
            if (triggerDataset.Tables.Count > 0 && triggerDataset.Tables[0].Rows.Count > 0)
            {
                return new DatabaseObject(objectName, "Trigger", "FOUND");
            }

            //  Hledám mezi Views
            if (_database.Views.Contains(objectName))
            {
                return new DatabaseObject(objectName, "View", "FOUND");
            }

            //  Hledám mezi user-defined functions
            if (_database.UserDefinedFunctions.Contains(objectName))
            {
                return new DatabaseObject(objectName, "Function", "FOUND");
            }

            return null; //nebyl nalezen
        }

        /// <summary>
        /// Získá seřazený seznam objektů databáze na základě jejich závislostí.
        /// </summary>
        /// <param name="objectNames">Seznam názvů objektů, které mají být seřazeny.</param>
        /// <returns>Seznam seřazených názvů objektů.</returns>
        public List<string> GetOrderedObjects(List<string> objectNames,CancellationToken token)
        {
            var objects = GetDatabaseObjectsWithDependencies(objectNames,token); // Pouze vybrané objekty
            Dictionary<string, List<string>> adjacencyList = new Dictionary<string, List<string>>();
            Dictionary<string, int> inDegree = new Dictionary<string, int>();


            //  Vytvoření grafu závislostí
            foreach (var obj in objects)
            {


                if (!adjacencyList.ContainsKey(obj.Name))
                    adjacencyList[obj.Name] = new List<string>();

                foreach (var dep in obj.Dependencies)
                {
                    if (!objectNames.Contains(dep)) // jenom mezi vybranými objekty
                        continue;

                    if (!adjacencyList.ContainsKey(dep))
                        adjacencyList[dep] = new List<string>();

                    adjacencyList[dep].Add(obj.Name);

                    if (!inDegree.ContainsKey(obj.Name))
                        inDegree[obj.Name] = 0;

                    if (!inDegree.ContainsKey(dep))
                        inDegree[dep] = 0;

                    inDegree[obj.Name]++;
                }
            }

            //  objekty bez závislostí
            foreach (var obj in objects)
            {
                if (!inDegree.ContainsKey(obj.Name))
                {
                    inDegree[obj.Name] = 0;
                    adjacencyList[obj.Name] = new List<string>();
                }
            }

            //  Topologické třídění
            Queue<string> queue = new Queue<string>();
            foreach (var obj in inDegree)
            {
                if (obj.Value == 0)
                    queue.Enqueue(obj.Key);
            }

            List<string> sortedObjects = new List<string>();
            while (queue.Count > 0)
            {
                string objName = queue.Dequeue();
                sortedObjects.Add(objName);

                if (!adjacencyList.ContainsKey(objName)) continue;

                foreach (var dependent in adjacencyList[objName])
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }

            return sortedObjects;
        }


        public List<DatabaseObject> GetDatabaseObjectsWithDependencies(List<string> objectNames, CancellationToken token)
        {
            var objects = new List<DatabaseObject>();
            if (objectNames.Count == 0) return objects;

            Console.WriteLine("načítání objektů se závislostmi (bulk)...");

            string inList = string.Join(",", objectNames.Select(n => $"'{n.Replace("'", "''")}'"));
            var nameSet = new HashSet<string>(objectNames, StringComparer.OrdinalIgnoreCase);

            //  Bulk dotaz #1: typ pro všechny názvy najednou
            var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string typeQuery = $@"
                SELECT name, type_desc
                FROM sys.objects
                WHERE name IN ({inList})";

            var typeDataset = _database.ExecuteWithResults(typeQuery);
            if (typeDataset.Tables.Count > 0)
            {
                foreach (System.Data.DataRow row in typeDataset.Tables[0].Rows)
                {
                    if (token.IsCancellationRequested) return objects;

                    string? name = row["name"]?.ToString();
                    string? typeDesc = row["type_desc"]?.ToString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(typeDesc)) continue;

                    string? friendly =
                        typeDesc.Contains("PROCEDURE") ? "Stored Procedure" :
                        typeDesc.Contains("VIEW") ? "View" :
                        typeDesc.Contains("TRIGGER") ? "Trigger" :
                        typeDesc.Contains("FUNCTION") ? "Function" :
                        null;

                    if (friendly != null) typeMap[name] = friendly;
                }
            }

            //  Bulk dotaz #2: dependencies (referencing → referenced) pro všechny najednou
            var depMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string depQuery = $@"
                SELECT referencing.name AS referencing_name,
                       d.referenced_entity_name AS referenced_name
                FROM sys.sql_expression_dependencies d
                INNER JOIN sys.objects referencing ON referencing.object_id = d.referencing_id
                WHERE referencing.name IN ({inList})";

            var depDataset = _database.ExecuteWithResults(depQuery);
            if (depDataset.Tables.Count > 0)
            {
                foreach (System.Data.DataRow row in depDataset.Tables[0].Rows)
                {
                    if (token.IsCancellationRequested) return objects;

                    string? refing = row["referencing_name"]?.ToString();
                    string? refed = row["referenced_name"]?.ToString();
                    if (string.IsNullOrEmpty(refing) || string.IsNullOrEmpty(refed)) continue;
                    if (!nameSet.Contains(refed)) continue; // jen v rámci výběru — stejně jako originál

                    if (!depMap.TryGetValue(refing, out var list))
                    {
                        list = new List<string>();
                        depMap[refing] = list;
                    }
                    list.Add(refed);
                }
            }

            //  Sestavení výsledku ve stejném pořadí jako vstup (kvůli stabilitě topologického sortu)
            foreach (var objName in objectNames)
            {
                if (token.IsCancellationRequested) return objects;
                if (!typeMap.TryGetValue(objName, out var type)) continue;

                var dbObject = new DatabaseObject(objName, type);
                if (depMap.TryGetValue(objName, out var deps))
                    dbObject.Dependencies = deps;
                objects.Add(dbObject);
            }

            Console.WriteLine($"Načteno {objects.Count}");
            return objects;
        }


        private string? GetObjectType(string objectName)
        {
            string query = $@"
                SELECT type_desc
                FROM sys.objects
                WHERE name = '{objectName}'
            ";

            var dataset = _database.ExecuteWithResults(query);
            if (dataset.Tables.Count == 0 || dataset.Tables[0].Rows.Count == 0)
                return null;

            string? typeDesc = dataset.Tables[0].Rows[0]["type_desc"]?.ToString();
            if (string.IsNullOrEmpty(typeDesc))
                return null;

            if (typeDesc.Contains("PROCEDURE")) return "Stored Procedure";
            if (typeDesc.Contains("VIEW")) return "View";
            if (typeDesc.Contains("TRIGGER")) return "Trigger";

            return null;
        }


        private List<string> GetObjectDependencies(string objectName, List<string> objectNames)
        {
            List<string> dependencies = new List<string>();

            string query = $@"
                SELECT referenced_entity_name
                FROM sys.sql_expression_dependencies
                WHERE referencing_id = OBJECT_ID('{objectName}')
            ";

                var dataset = _database.ExecuteWithResults(query);
                if (dataset.Tables.Count == 0 || dataset.Tables[0].Rows.Count == 0)
                    return dependencies;

                var table = dataset.Tables[0];

                foreach (System.Data.DataRow row in table.Rows)
                {
                    string? dependency = row["referenced_entity_name"]?.ToString();
                    if (!string.IsNullOrEmpty(dependency) && objectNames.Contains(dependency))
                    {
                        dependencies.Add(dependency);
                        Console.WriteLine($"{objectName} závisí na {dependency}");
                    }
                }


            return dependencies;
        }


        public List<string> GetAllDatabases()
        {
            var databaseNames = new List<string>();

            var query = "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' AND name NOT IN ('master', 'tempdb', 'model', 'msdb')";
            var result = _dbConnection.ServerInstance?.ConnectionContext.ExecuteWithResults(query);

            if (result == null) return databaseNames;

            foreach (System.Data.DataRow row in result.Tables[0].Rows)
            {
                var name = row["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    databaseNames.Add(name);
            }

            return databaseNames;
        }


        /// <summary>
        /// Vrátí (name, type) pro všechny objekty zapsané v coal_instalObjects v jednom dotazu.
        /// Filtruje na procedury, views, triggery a funkce — typy podporované Update workflow.
        /// </summary>
        public List<(string Name, string Type)> GetInstalObjectsWithTypes(CancellationToken token)
        {
            var result = new List<(string Name, string Type)>();

            string query = @"
                SELECT o.name, o.type_desc
                FROM sys.objects o
                INNER JOIN coal_instalObjects i
                    ON LTRIM(RTRIM(i.nazev)) = o.name COLLATE DATABASE_DEFAULT
                WHERE o.type IN ('P', 'V', 'TR', 'FN', 'IF', 'TF')
                ORDER BY o.type_desc, o.name";

            var dataset = _database.ExecuteWithResults(query);
            if (dataset.Tables.Count == 0) return result;

            foreach (System.Data.DataRow row in dataset.Tables[0].Rows)
            {
                if (token.IsCancellationRequested) return result;

                string? name = row["name"]?.ToString();
                string? typeDesc = row["type_desc"]?.ToString();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(typeDesc)) continue;

                string friendly =
                    typeDesc.Contains("PROCEDURE") ? "Stored Procedure" :
                    typeDesc.Contains("VIEW") ? "View" :
                    typeDesc.Contains("TRIGGER") ? "Trigger" :
                    typeDesc.Contains("FUNCTION") ? "Function" :
                    typeDesc;

                result.Add((name, friendly));
            }

            return result;
        }


        public List<string> GetObjectsFromTable(CancellationToken token)
        {
            var objectNames = new List<string>();

            string query = "select nazev from coal_instalObjects";

            var result = _database.ExecuteWithResults(query);

            if (result.Tables.Count > 0 && result.Tables[0].Rows.Count > 0)
            {
                foreach (System.Data.DataRow row in result.Tables[0].Rows)
                {
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine("přerušeno.");
                        return new List<string>();
                    }
                    var name = row["nazev"].ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        objectNames.Add(name.Trim());
                }
            }

            return objectNames;

        }


        public List<string> GetObjectsFromTable(string table, string column)
        {
            var objectNames = new List<string>();

            string query = $"select {column} from {table}";

            var result = _database.ExecuteWithResults(query);

            if (result.Tables.Count > 0 && result.Tables[0].Rows.Count > 0)
            {
                foreach (System.Data.DataRow row in result.Tables[0].Rows)
                {
                    var name = row[column].ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        objectNames.Add(name.Trim());
                }
            }

            return objectNames;

        }

        public List<CoalshopItem> GetCoalshopList()
        {
            var result = new List<CoalshopItem>();
            string query = "SELECT id_externi_shop, nazev FROM Coalshop ORDER BY nazev";
            try
            {
                var dataset = _database.ExecuteWithResults(query);
                if (dataset.Tables.Count > 0)
                {
                    foreach (System.Data.DataRow row in dataset.Tables[0].Rows)
                    {
                        if (int.TryParse(row["id_externi_shop"]?.ToString(), out int id))
                        {
                            string nazev = row["nazev"]?.ToString() ?? id.ToString();
                            result.Add(new CoalshopItem(id, $"{nazev} ({id})"));
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        public Dictionary<string, List<System.Data.DataRow>> GetMappingValues(int shopId, int defaultOrder)
        {
            // Tabulky kde filtrujeme i podle default_order
            var tablesWithDefaultOrder = new[]
            {
                "COAL_orderHeader_mapping",
                "COAL_orderHeader_mapping_ext",
                "COAL_orderProducts_mapping",
                "COAL_payOrder_mapping",
                "COAL_orderProducts_mapping_ext",
            };

            // Tabulky bez default_order (jen podle id_externi_shop)
            var tablesWithoutDefaultOrder = new[]
            {
                ("Coal_shoptetProductMap",         "id_externi_shop"),
                ("COAL_createCompany_mapping_ext", "id_externi_shop"),
                ("COAL_createCompany_mapping",     "id_externi_shop"),
                ("COAL_createProduct_mapping",     "id_externi_shop"),
                ("COAL_createProduct_mapping_ext", "id_externi_shop"),
                ("Coal_tabkmen_ext_mapping",       "IDCoalshop"),
            };

            var result = new Dictionary<string, List<System.Data.DataRow>>();

            foreach (var table in tablesWithDefaultOrder)
            {
                try
                {
                    string query = $"SELECT * FROM {table} WHERE id_externi_shop = {shopId} AND default_order = {defaultOrder}";
                    var dataset = _database.ExecuteWithResults(query);
                    result[table] = dataset.Tables.Count > 0
                        ? dataset.Tables[0].Rows.Cast<System.Data.DataRow>().ToList()
                        : new List<System.Data.DataRow>();
                }
                catch
                {
                    result[table] = new List<System.Data.DataRow>();
                }
            }

            foreach (var (table, idCol) in tablesWithoutDefaultOrder)
            {
                try
                {
                    string query = $"SELECT * FROM {table} WHERE {idCol} = {shopId}";
                    var dataset = _database.ExecuteWithResults(query);
                    result[table] = dataset.Tables.Count > 0
                        ? dataset.Tables[0].Rows.Cast<System.Data.DataRow>().ToList()
                        : new List<System.Data.DataRow>();
                }
                catch
                {
                    result[table] = new List<System.Data.DataRow>();
                }
            }

            return result;
        }


    }
}
