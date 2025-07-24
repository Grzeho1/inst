using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using inst.Enums;
using Microsoft.SqlServer.Management.Smo.Wmi;
using System.IO;
using Microsoft.SqlServer.Management.Smo;

namespace inst
{
    internal class GlobalConfig
    {
        public static KonektorEnums.Konektor SelectedKonektor { get; set; }

        public static readonly string ServerIP = "172.16.131.81";

        public static readonly string UniverzalDatabase = "HeliosKonektor001";
        public static readonly string ShoptetDatabase = "HeliosKonektor000";

        public static readonly string GitPushFolderShoptet = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update", "auto.ps1");
        public static readonly string GitPushFolderUniverzal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update", "auto.ps1");

        public static readonly string exportFolderPathShoptet = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update", "sql");
        public static readonly string exportFolderPathUniverzal= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update", "sql");

    }
}
