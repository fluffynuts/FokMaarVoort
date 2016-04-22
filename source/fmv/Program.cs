using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using PeanutButter.Utils;

namespace fmv
{
    class Program
    {
        private static string _returnFolder;
        private static readonly List<string> _lastLines = new List<string>();
        static void Main(string[] args)
        {
            StoreConsoleColor();
            ChangeWorkingFolderIfNecessary(args);
            string result = null;
            do
            {
                if (result != null)
                {
                    WriteBlue(">>> Issues persist! Round and round we go! <<<");
                }
                result = FokMaarVoort();
            } while ((result ?? "").Contains("CONFLICT"));
            ChangeBackWorkingFolderIfNecessary();
        }

        private static void StoreConsoleColor()
        {
            _originalColor = Console.ForegroundColor;
        }

        private static readonly Dictionary<string, Action<string>> _strategies = new Dictionary<string, Action<string>>()
        {
            { "t", CheckoutTheirs },
            { "o", CheckoutOurs },
            { "i", CheckoutInteractive }
        };

        private static string FokMaarVoort()
        {
            var files = GetFilesWhichNeedMerge();
            WriteYellow();
            WriteYellow(">>>");
            WriteYellow("Files which \"need merge\":");
            files.ForEach(WriteCyan);
            WriteYellow();
            string userChoice;
            do
            {
                WriteYellow("What to do? Take all (T)heirs ? Take all (O)urs ? (I)nteractive");
                userChoice = GetOneCharUserChoice();
            } while (!_strategies.Keys.Contains(userChoice));


            files.ForEach(CheckoutTheirs);
            GitAddAllRecursive();
            return AttemptRebaseContinue();
        }

        private static void CheckoutInteractive(string path)
        {
            string answer;
            var acceptable = new[] { "o", "t" };
            do
            {
                WriteRed($"(O)urs or (T)heirs for: {path}");
                answer = GetOneCharUserChoice();
            } while (!acceptable.Contains(answer));
            _strategies[answer](path);
        }

        private static string GetOneCharUserChoice()
        {
            var result = Console.ReadLine().Trim().ToLower();
            return result.Length > 1 ? result.Substring(0,1): result;
        }

        private static void CheckoutOurs(string path)
        {
            WriteMagenta("Checking out ours: " + path);
            RunGit("checkout", "--ours", $"\"{path}\"");
        }

        private static void CheckoutTheirs(string path)
        {
            WriteCyan("Checking out theirs: " + path);
            RunGit("checkout", "--theirs", $"\"{path}\"");
        }

        private static void WriteMagenta(string message)
        {
            WriteColor(ConsoleColor.Magenta, message);
        }

        private static void WriteYellow(string message = "")
        {
            WriteColor(ConsoleColor.Yellow, message);
        }

        private static void WriteBlue(string message)
        {
            WriteColor(ConsoleColor.Blue, message);
        }

        private static void WriteColor(ConsoleColor color, string message)
        {
            using (new AutoResetter(() => SetConsoleForeGround(color), RestoreConsoleColor))
            {
                Console.WriteLine(message);
            }
        }

        private static void WriteRed(string message)
        {
            WriteColor(ConsoleColor.Red, message);
        }

        private static void WriteCyan(string message)
        {
            WriteColor(ConsoleColor.Cyan, message);
        }

        private static void RestoreConsoleColor()
        {
            Console.ForegroundColor = _originalColor;
        }

        private static ConsoleColor _originalColor;

        private static void SetConsoleForeGround(ConsoleColor color)
        {
            _originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
        }

        private static string AttemptRebaseContinue()
        {
            WriteYellow("Attempting to continue rebase");
            var result = RunGit("rebase", "--continue");
            return result;
        }

        private static void GitAddAllRecursive()
        {
            WriteYellow("Adding all changes...");
            RunGit("add", "-A", ":/");
        }

        private static IEnumerable<string> GetFilesWhichNeedMerge()
        {
            var first = RunGit("rebase", "--continue");
            var lines = first.Split(new[] {"\n", "\r"}, StringSplitOptions.RemoveEmptyEntries);
            var interestingLines = lines.Where(l => l.EndsWith(": needs merge"));
            var files = interestingLines.Select(l => l.Replace(": needs merge", ""));
            return files;
        }

        private static void ChangeBackWorkingFolderIfNecessary()
        {
            if (_returnFolder != null)
                Directory.SetCurrentDirectory(_returnFolder);
        }

        private static void ChangeWorkingFolderIfNecessary(string[] args)
        {
            if (args.Any())
            {
                _returnFolder = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(args.First());
            }
        }

        static string RunGit(params string[] args)
        {
            return RunCmd("git", args);
        }

        static string RunCmd(string cmd, params string[] args)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(cmd)
            {
                Arguments = string.Join(" ", args),
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _lastLines.Clear();
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += Tee;
            process.ErrorDataReceived += Tee;
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            while (!process.HasExited)
            {
                Thread.Sleep(100);
            }
            process.WaitForExit();
            var result = string.Join("\n", _lastLines);
            _lastLines.Clear();
            return result;
        }

        private static void Tee(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _lastLines.Add(e.Data);
                Console.WriteLine(e.Data);
            }
        }
    }
}
