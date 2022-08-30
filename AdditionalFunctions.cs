using Microsoft.ClearScript;

using System;
using System.IO;
using System.Reflection;

namespace R8
{
    public static class ArrayExtensions
    {
        public static object ToScriptArray(this Array array, ScriptEngine engine)
        {
            return engine.Script.Array.from(array);
        }
    }

    internal static class AdditionalFunctions
    {
        public static void Add()
        {
            Program.v8.Script.print = (Action<object>)Console.WriteLine;
            Program.v8.Script.exit = (Action<int>)Environment.Exit;
            Program.v8.Script.attach = (Func<string, bool>)Attach;
            Program.v8.Script.attachNamed = (Func<string, string, bool>)AttachNamed;
            Program.v8.Script.glob = (Func<string, string[]>)Glob;
            Program.v8.Script.globall = (Func<string, string[]>)GlobAll;
            Program.v8.Script.include = (Func<string, string>)Include;
            Program.v8.Script.readline = (Func<string>)Console.ReadLine;
            Program.v8.Script.slurp = (Func<string, string>)File.ReadAllText;
            Program.v8.Script.inhale = (Func<string, string>)Inhale;
            Program.v8.Script.die = (Action<string>)Die;
            Program.v8.Script.plugin = (Func<string, bool>)Plugin;
            Program.v8.Script.systypeof = (Func<object, Type>)SysTypeOf;
            Program.v8.Script.assembly = (Func<string, string, bool>)AddNamedAssembly;
            Program.v8.Script.toArray = (Func<Array, object>)ToArray;
        }

        private static Type SysTypeOf(object arg)
        {
            return arg.GetType();
        }

        private static object ToArray(Array arg)
        {
            return arg.ToScriptArray(Program.v8);
        }


        private static string Inhale(string arg)
        {
            using FileStream stream = File.Open(arg, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();
            return result;
        }

        private static bool AttachNamed(string dllPath, string name = "")
        {
            var status = false;
            var htc = new HostTypeCollection();
            try
            {
                /// var assem = System.Reflection.Assembly.LoadFrom(dllPath);
                var assem = Assembly.Load(AssemblyName.GetAssemblyName(dllPath));
                htc.AddAssembly(assem);
                Program.v8.AddHostObject(name, HostItemFlags.GlobalMembers, htc); //FIXME checkout the hosttypes
                Console.Error.WriteLine($"Attached {dllPath} as {name}");
                status = true;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var item in rtle.LoaderExceptions)
                {
                    Console.Error.WriteLine(item.Message);

                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.Error.WriteLine(fnfe.Message);


            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);

            }
            return status;
        }

        private static bool AddNamedAssembly(string assemblyName, string internalName)
        {
            var status = false;
            var htc = new HostTypeCollection();
            try
            {
                htc.AddAssembly(assemblyName);
                Program.v8.AddHostObject(internalName, htc);
                Console.Error.WriteLine($"Attached {assemblyName} as {internalName}");
                status = true;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var item in rtle.LoaderExceptions)
                {
                    Console.Error.WriteLine(item.Message);

                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
            return status;
        }

        private static bool Plugin(string dllPath)
        {
            var status = false;
            var htc = new HostTypeCollection();
            try
            {
                var assem = System.Reflection.Assembly.LoadFrom(dllPath);
                ///var assem = Assembly.LoadFrom(AssemblyName.GetAssemblyName(dllPath));
                htc.AddAssembly(assem);
                string name = assem.FullName.Split(',')[0];

                Program.v8.AddHostObject(name, htc); //FIXME checkout the hosttypes
                Console.Error.WriteLine($"Attached {dllPath} as {name}");
                status = true;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var item in rtle.LoaderExceptions)
                {
                    Console.Error.WriteLine(item.Message);
                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.Error.WriteLine(fnfe.Message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
            return status;

        }

        private static bool Attach(string dllPath, string name = "")
        {
            var status = false;
            var htc = new HostTypeCollection();
            try
            {
                var assem = System.Reflection.Assembly.LoadFrom(dllPath);
                /// var assem = Assembly.LoadFrom(AssemblyName.GetAssemblyName(dllPath));
                htc.AddAssembly(assem);
                if (name.Length == 0)
                {
                    name = assem.FullName.Split(',')[0];
                }

                Program.v8.AddHostObject(name, htc); //FIXME checkout the hosttypes
                Console.Error.WriteLine($"Attached {dllPath} as {name}");
                status = true;
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var item in rtle.LoaderExceptions)
                {
                    Console.Error.WriteLine(item.Message);
                }

            }
            catch (FileNotFoundException fnfe)
            {
                Console.Error.WriteLine(fnfe.Message);

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);

            }
            return status;
        }
        private static bool Attach(string obj) => Attach(obj, string.Empty);

        private static string[] Glob(string wild)
        {
            var path = Path.GetDirectoryName(wild);
            if (path.Length == 0)
            {
                path = ".\\";
            }
            wild = Path.GetFileName(wild);
            return Directory.GetFiles(path, wild);
        }

        private static string[] GlobAll(string wild)
        {
            var path = Path.GetDirectoryName(wild);
            if (path.Length == 0)
            {
                path = ".\\";
            }
            wild = Path.GetFileName(wild);
            return Directory.GetFiles(path, wild, SearchOption.AllDirectories);
        }


        private static string Include(string arg)
        {
            if (File.Exists(arg))
            {
                try
                {
                    var text = File.ReadAllText(arg);
                    Program.v8.Execute(text);
                    return text;
                }
                catch (ScriptEngineException see)
                {
                    Console.Error.WriteLine(see.Message);
                    return see.Message;
                }
            }
            else
            {
                Console.Error.WriteLine($"{arg} not found.");
                return arg + " not found.";
            }
        }

        private static void Die(string message = "")
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }

    }
}
