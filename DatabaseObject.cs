using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inst
{
    /// <summary>
    /// Represents a database object with a name, type, status, and dependencies.
    /// </summary>
    public class DatabaseObject
    {
      
        public string Name { get; set; }

        public string Type { get; set; }

      
        public string Status { get; set; }

        public List<string> Dependencies { get; set; }

        public DatabaseObject(string name, string type, string status = "")
        {
            Name = name;
            Type = type;
            Status = status;
            Dependencies = new List<string>();
        }
        public DatabaseObject() { }

    }

}
