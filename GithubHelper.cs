using inst;
using inst.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace inst
{
    class GithubHelper
    {
        private readonly string _repoPath;
        private readonly string _folderToCommit;
        private readonly string _remoteUrl = "git@github.com:Grzeho1/sql.git";
        private readonly string _branch = "main";
        private readonly string _relativeFolder;

        private string SshFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        private string PrivateKey => Path.Combine(SshFolder, "id_ed25519");
        private string PublicKey => PrivateKey + ".pub";


        public GithubHelper()
        {
            _repoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db-update");

            string konektorFolder = GlobalConfig.SelectedKonektor switch
            {
                KonektorEnums.Konektor.Shoptet => "shoptet",
                KonektorEnums.Konektor.Univerzal => "univerzal",
                _ => throw new Exception("Neznámý konektor.")
            };

            _folderToCommit = konektorFolder;
        }
        public void Run()
        {
            Directory.SetCurrentDirectory(_repoPath);
            EnsureSshKey();
            EnsureSshAgent();
            EnsureGitRepo();
            SetRemote();
            CommitAndPush();
        }

        private void EnsureSshKey()
        {
            if (!File.Exists(PrivateKey))
            {
                Console.WriteLine("[INFO] SSH klíč nebyl nalezen – generuji...");
                Run("ssh-keygen", $"-t ed25519 -C \"{Environment.UserName}@{Environment.MachineName}\" -f \"{PrivateKey}\" -q");

                if (File.Exists(PublicKey))
                {
                    string pubKey = File.ReadAllText(PublicKey);
                    Console.WriteLine("\n[INFO] Veřejný klíč:");
                    Console.WriteLine(pubKey);
                    Console.WriteLine("Přidej ho na GitHub a stiskni Enter...");
                  //  Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("[ERROR] SSH klíč se nepodařilo vytvořit.");
                  //  Console.ReadLine();
                    Environment.Exit(1);
                }
            }
        }

        private void EnsureSshAgent()
        {
            if (Process.GetProcessesByName("ssh-agent").Length == 0)
            {
                Run("ssh-agent", "-s");
            }

            Run("ssh-add", $"\"{PrivateKey}\"");
        }

        private void EnsureGitRepo()
        {
            if (!Directory.Exists(Path.Combine(_repoPath, ".git")))
            {
                Console.WriteLine("[INFO] Inicializuji nový Git repozitář...");
                Run("git", "init");
                Run("git", $"remote add origin {_remoteUrl}");
                Run("git", $"checkout -b {_branch}");
                Run("git", "commit --allow-empty -m \"Initial commit\"");
            }
        }

        private void SetRemote()
        {
            string currentRemote = RunOutput("git", "remote get-url origin").Trim();
            if (currentRemote.StartsWith("https://github.com/"))
            {
                string sshUrl = currentRemote.Replace("https://github.com/", "git@github.com:");
                Run("git", $"remote set-url origin {sshUrl}");
            }
            else
            {
                Run("git", $"remote set-url origin {_remoteUrl}");
            }

            Console.WriteLine($"[INFO] Remote origin nastaven na: {_remoteUrl}");
        }

        private void CommitAndPush()
        {
            string fullPath = Path.Combine(_repoPath, _folderToCommit);
            if (!Directory.Exists(fullPath))
            {
                Console.WriteLine($"[ERROR] Složka {fullPath} neexistuje.");
                return;
            }

            Run("git", $"add \"{_folderToCommit}\"");
            string changes = RunOutput("git", "status --porcelain");

            if (!string.IsNullOrWhiteSpace(changes))
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Run("git", $"commit -m \"Auto commit SQL změn - {timestamp}\"");
                Run("git", $"push origin {_branch} --force");
                Console.WriteLine("[OK] Změny byly odeslány.");
            }
            else
            {
                Console.WriteLine("[INFO] Žádné změny k odeslání.");
            }
        }

        private int Run(string exe, string args)
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode;
        }

        private string RunOutput(string exe, string args)
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output;
        }
    }


}
