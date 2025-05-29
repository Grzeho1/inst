using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace inst
{
    /// <summary>
    /// Pomocná třída pro práci se soubory.
    /// </summary>
    public static class FileHelper
    {
        private static readonly string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "objects_to_load.txt");

        /// <summary>
        /// Načte názvy objektů ze souboru.
        /// </summary>
        /// <returns>Seznam názvů objektů.</returns>
        public static List<string> LoadObjectNames()
        {
            if (!File.Exists(filePath))
                return new List<string>();

            return new List<string>(File.ReadAllLines(filePath,Encoding.UTF8));
        }

        public static int GetObjectNamesCount()
        {
            if (!File.Exists(filePath))
                return 0;

            return File.ReadAllLines(filePath).Length;
        }

        /// <summary>
        /// Spojí všechny SQL soubory ve složce do jednoho souboru.
        /// </summary>
        /// <param name="sourceFolder">Cesta ke složce se zdrojovými SQL soubory.</param>
        /// <param name="outputFile">Cesta k výstupnímu souboru.</param>
        public static void MergeSqlFiles(string sourceFolder, string outputFile)
        {
            try
            {
                var sqlFiles = Directory.GetFiles(sourceFolder, "*.sql");

                var sortedFiles = sqlFiles
                    .Select(file => new { FileName = file, Order = ExtractOrderFromFileName(file) })
                    .Where(f => f.Order != -1)
                    .OrderBy(f => f.Order)
                    .Select(f => f.FileName)
                    .ToList();

                if (sortedFiles.Count == 0)
                {
                    MessageBox.Show("Nebyly nalezeny žádné SQL soubory", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StringBuilder mergedScript = new StringBuilder();

                foreach (var file in sortedFiles)
                {
                    mergedScript.AppendLine(File.ReadAllText(file));
                    mergedScript.AppendLine("\nGO\n");
                }

                File.WriteAllText(outputFile, mergedScript.ToString(), Encoding.UTF8);

                MessageBox.Show($"Spojeno {sortedFiles.Count} souborů do {outputFile}.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Chyba:\n" + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Získá číslo na začátku názvu souboru.
        /// </summary>
        /// <param name="filePath">Cesta k souboru.</param>
        /// <returns>Číslo na začátku názvu souboru nebo -1, pokud není platné.</returns>
        private static int ExtractOrderFromFileName(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string[] parts = fileName.Split('_');
            if (parts.Length > 1 && int.TryParse(parts[0], out int order))
            {
                return order;
            }
            return -1;
        }
     

        
    }
}

