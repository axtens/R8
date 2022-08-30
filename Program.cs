
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace R8
{
    static class Program
    {
        public static V8ScriptEngine v8;
        public static readonly Dictionary<string, object> Settings = new();
        public static List<string> arguments = new();
        public static List<string> parameters = new();

        static int Main(string[] args)
        {
            bool binDebug = false;
            bool debug = false;

            if (args.Length == 0)
            {
                Console.Error.WriteLine("R8 {run|repl} {script} [--bindebug] [-- args...]");
                return -1;
            }
            var argsList = new List<string>();
            argsList.AddRange(args);

            if (argsList.ElementAt(0) != "run" && argsList.ElementAt(0) != "repl")
            {
                Console.Error.WriteLine("run or repl");
                return -1;
            }

            if (argsList.Count == 1 || argsList.ElementAt(1).StartsWith("--"))
            {
                argsList.Insert(1, "");
            }

            int i = 2;

            while (i < argsList.Count)
            {
                if (argsList?[i] == "--")
                {
                    argsList.RemoveAt(i);
                    while (i < argsList.Count)
                    {
                        arguments.Add(argsList[i]);
                        argsList.RemoveAt(i);
                    }
                    break;
                }

                if (argsList[i].StartsWith("--"))
                {
                    parameters.Add(argsList[i]);
                    argsList.RemoveAt(i);
                }
                else
                {
                    /// nothing else allowed
                    Console.WriteLine($"ignoring {argsList[i]}");
                    argsList.RemoveAt(i);
                }
            }

            if (parameters.Contains("--bindebug"))
            {
                binDebug = true;
            }

            if (parameters.Contains("--debug"))
            {
                Console.WriteLine("Launch Chrome. Navigate to chrome://inspect");
                debug = true;
            }

            if (binDebug)
            {
                System.Diagnostics.Debugger.Launch();
            }

            var V8Setup = V8ScriptEngineFlags.EnableDebugging |
                V8ScriptEngineFlags.EnableRemoteDebugging |
                V8ScriptEngineFlags.DisableGlobalMembers;

            if (debug) V8Setup |= V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart;

            string cmd = argsList[0];
            string script = argsList[1];
            if (string.IsNullOrEmpty(script) && cmd == "run")
            {
                Console.WriteLine("No script.");
                return -1;
            }

            if (cmd == "run" && !File.Exists(script))
            {
                Console.WriteLine($"{script} not found.");
                return -1;

            }

            var r8Temp = Path.Combine(Path.GetTempPath(), "R8");
            Directory.CreateDirectory(r8Temp);
            string replFile = Path.Combine(r8Temp, $"repl_{DateTime.UtcNow:yyyy'-'MM'-'dd'-'HH'-'mm'-'ss'-'fff}.txt"); // eventually timestamped

            v8 = new V8ScriptEngine(V8Setup, 9229);
            SetupDoubleUnderscoreValues(arguments);

            LoadGlobalFunctions();
            AdditionalFunctions.Add();

            ParseAutoLoadItems(v8, v8.Script.__.autoloadPath);

            if (cmd == "run")
            {
                var context = v8.Compile(File.ReadAllText(script));
                object evaluand = v8.Evaluate(context);
                if (evaluand.GetType() != typeof(VoidResult) && evaluand.GetType() != typeof(Undefined))
                {
                    Console.WriteLine($"{evaluand}");
                }
            }
            if (cmd == "repl")
            {
                if (script != "" && File.Exists(script))
                {
                    EvaluateBeforeRepl(script);
                }
                Console.WriteLine($"Logging to {replFile}");
                RunREPL(replFile);
            }
            return 0;
        }

        private static void EvaluateBeforeRepl(string script)
        {
            var context = v8.Compile(File.ReadAllText(script));
            object evaluand = v8.Evaluate(context);
            if (evaluand.GetType() != typeof(Microsoft.ClearScript.VoidResult))
            {
                Console.WriteLine($"{evaluand}");
            }
        }

        private static void ParseAutoLoadItems(V8ScriptEngine v8, string autoloadPath)
        {
            var scriptFiles = from string script in Directory.GetFiles(autoloadPath, "*.*")
                              where script.EndsWith(".js") || script.EndsWith(".r8") || script.EndsWith(".r8s")
                              select script;
            foreach (var scriptFile in scriptFiles)
            {
                v8.Evaluate(File.ReadAllText(scriptFile));
            }

        }

        private static void SetupDoubleUnderscoreValues(List<string> args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            v8.Script.__ = new PropertyBag();
            v8.Script.__.V8 = v8;
            v8.Script.__.arg = new PropertyBag();
            v8.Script.__.argc = args.Count;

            var i = 0;
            foreach (var a in from string key in args select key)
            {
                v8.Script.__.arg[i] = a;
                i++;
            }

            v8.Script.__.version = Build_version();
            v8.Script.__.binaryPathFile = Assembly.GetExecutingAssembly().Location;
            v8.Script.__.domain = Environment.GetEnvironmentVariable("USERDOMAIN");
            v8.Script.__.userName = Environment.GetEnvironmentVariable("USERNAME");
            v8.Script.__.userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            v8.Script.__.processId = Environment.ProcessId;

            var binary = new System.IO.DirectoryInfo(v8.Script.__.binaryPathFile);
            v8.Script.__.autoloadPath = Path.Combine(binary.Parent.FullName, "AutoLoad");
            v8.Script.__.pluginsPath = Path.Combine(binary.Parent.FullName, "Plugins");
            v8.Script.__.settingsPath = Path.Combine(binary.Parent.FullName, "Settings");
            v8.Script.__.libraryPath = Path.Combine(binary.Parent.FullName, "Library");
            v8.Script.__.binaryPath = binary.Parent.FullName;

            Directory.CreateDirectory(v8.Script.__.autoloadPath);
            Directory.CreateDirectory(v8.Script.__.libraryPath);
            Directory.CreateDirectory(v8.Script.__.pluginsPath);
            Directory.CreateDirectory(v8.Script.__.settingsPath);

            var startTime = Process.GetCurrentProcess().StartTime;
            v8.Script.__.startTime = DateTime.UtcNow.ToString("o"); // v8.Evaluate($"new Date({startTime.Year},{startTime.Month - 1},{startTime.Day},{startTime.Hour},{startTime.Minute},{startTime.Second},{startTime.Millisecond})");

            v8.Script.__.prompt = "'R8> '";
        }

        private static string Build_version()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var Value = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.MinorRevision * 2);
            return string.Format("{0} [{1}]", version.ToString(), Value.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss"));
        }

        private static void RunREPL(string fileName)
        {
            while (true)
            {
                Console.Write(v8.Evaluate(v8.Script.__.prompt));
                string cmd = Console.ReadLine();
                if (cmd == "exit")
                {
                    break;
                }
                if (fileName != string.Empty)
                {
                    File.AppendAllText(fileName, cmd + "\r\n");
                }

                object evaluand;
                try
                {
                    evaluand = v8.Evaluate(cmd);
                    /// Type type = evaluand.GetType();
                }
                catch (ScriptEngineException see)
                {
                    evaluand = "";
                    Console.WriteLine(see.Message);
                }
                catch (NullReferenceException nre)
                {
                    evaluand = "";
                    Console.WriteLine(nre.Message);
                }
                catch (Exception e)
                {
                    evaluand = "";
                    Console.WriteLine(e.Message);
                }
                if (evaluand == null)
                {
                    Console.WriteLine($"{evaluand}");
                    File.AppendAllText(fileName, $"// {evaluand}\r\n");
                }
                else if (evaluand.GetType() != typeof(Microsoft.ClearScript.VoidResult))
                {
                    Console.WriteLine($"{evaluand}");
                    if (fileName != string.Empty)
                    {
                        File.AppendAllText(fileName, $"// {evaluand}\r\n");
                    }
                }
            }
        }

        private static void LoadGlobalFunctions()
        {
            v8.AddHostObject("CS$ExtendedHostFunctions", new ExtendedHostFunctions());
            v8.AddHostObject("CS$HostFunctions", new HostFunctions());
            v8.AddHostObject("$", new HostTypeCollection("mscorlib",
                                                            "System",
                                                            "System.Core",
                                                            "System.Data",
                                                            "System.Net"));
        }
    }
}
