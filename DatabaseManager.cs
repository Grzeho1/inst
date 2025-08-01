﻿using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Shapes;

namespace inst
{
    /// <summary>
    /// Manages database operations such as retrieving and exporting database objects.
    /// </summary>
    public class DatabaseManager
    {
        private readonly DatabaseConnection _dbConnection;
        //private Database? _database => _dbConnection.SelectedDatabase;
        private readonly Database _database;
        private CancellationTokenSource _cancellationTokenSource;
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
                objects.Add(new DatabaseObject(view.Name, "View"));
            }

            return objects;
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

            var sortedObjects = GetOrderedObjects(objectNames,token);
            HashSet<string> usedFileNames = new HashSet<string>(); // Sledování použitých názvů

            int order = 1;
            foreach (var objName in sortedObjects)
            {
                string sqlText = GetObjectText(objName);

                if (!string.IsNullOrEmpty(sqlText))
                {
                    string fileName = $"{order}_{objName}.sql";
                    //  string fileName = $"{objName}.sql";

                    // Kontrola, zda už tento název není použitý
                    while (usedFileNames.Contains(fileName))
                    {
                        order++;
                        fileName = $"{order}_{objName}.sql";
                        // fileName = $"{objName}.sql";
                    }

                    string filePath = System.IO.Path.Combine(exportFolderPath, fileName);
                    File.WriteAllText(filePath, sqlText);
                    Console.WriteLine($" Exported: {filePath}");

                    usedFileNames.Add(fileName); // Přidáme název do sledovaných
                    order++;
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
        public DatabaseObject GetDatabaseObject(string objectName, CancellationToken token)
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

        /// <summary>
        /// Získá objekty databáze spolu s jejich závislostmi.
        /// </summary>
        /// <param name="objectNames">Seznam názvů objektů, které mají být získány se závislostmi.</param>
        /// <returns>Seznam objektů databáze se závislostmi.</returns>
        public List<DatabaseObject> GetDatabaseObjectsWithDependencies(List<string> objectNames,CancellationToken token)
        {
            List<DatabaseObject> objects = new List<DatabaseObject>();

            Console.WriteLine("načítání objektů se závislostmi...");
        
            foreach (var objName in objectNames)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("přerušeno.");
                    return objects;
                }

                string objectType = GetObjectType(objName);

                if (!string.IsNullOrEmpty(objectType))
                {
                    var dbObject = new DatabaseObject(objName, objectType);
                    dbObject.Dependencies = GetObjectDependencies(objName, objectNames);
                    objects.Add(dbObject);
                }
            }

            Console.WriteLine($"Načteno {objects.Count}");

            return objects;
        }

        /// <summary>
        /// Získá typ specifikovaného objektu databáze.
        /// </summary>
        /// <param name="objectName">Název objektu databáze.</param>
        /// <returns>Typ objektu.</returns>
        private string GetObjectType(string objectName)
        {
            string query = $@"
                SELECT type_desc 
                FROM sys.objects 
                WHERE name = '{objectName}'
            ";

            var dataset = _database.ExecuteWithResults(query);
            if (dataset.Tables.Count == 0 || dataset.Tables[0].Rows.Count == 0)
                return null;

            string typeDesc = dataset.Tables[0].Rows[0]["type_desc"].ToString();

            if (typeDesc.Contains("PROCEDURE")) return "Stored Procedure";
            if (typeDesc.Contains("VIEW")) return "View";
            if (typeDesc.Contains("TRIGGER")) return "Trigger";

            return null;
        }

        /// <summary>
        /// Získá závislosti specifikovaného objektu databáze.
        /// </summary>
        /// <param name="objectName">Název objektu databáze.</param>
        /// <param name="objectNames">Seznam názvů objektů, proti kterým se kontrolují závislosti.</param>
        /// <returns>Seznam závislostí.</returns>
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
                    string dependency = row["referenced_entity_name"].ToString();
                    if (objectNames.Contains(dependency)) //  Jen pokud je v seznamu exportovaných objektů
                    {
                        dependencies.Add(dependency);
                        Console.WriteLine($"{objectName} závisí na {dependency}");
                    }
                }
            
            
            return dependencies;
        }

        /// <summary>
        /// Získá názvy všech databází na serveru, kromě systémových databází.
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllDatabases()
        {
            var databaseNames = new List<string>();

            var query = "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' AND name NOT IN ('master', 'tempdb', 'model', 'msdb')";
            var result = _dbConnection.ServerInstance.ConnectionContext.ExecuteWithResults(query);

            foreach (System.Data.DataRow row in result.Tables[0].Rows)
            {
                databaseNames.Add(row["name"].ToString());
            }

            return databaseNames;
        }

        /// <summary>
        /// Získá názvy všech objektů z tabulky `coal_instalObjects` v databázi.
        /// </summary>
        /// <returns></returns>
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


    }
}
