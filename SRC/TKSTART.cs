namespace TKServer {
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.ServiceProcess;

    using TKServer.BASE;
    using TKServer.FormView;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TKSTART" />
    /// </summary>
    internal static class TKSTART {
        /// <summary>
        /// The Main
        /// </summary>
        [STAThread]
        internal static void Main(string[] args) {
            AssemblyLoadContext.Default.Resolving += ResolveManagedAssembly;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveUnmanagedDll;

            if (ShouldRunAsRuntimeProcess(args)) {
                RunRuntimeProcess(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();

            using (TK_StartSplash splash = new TK_StartSplash()) {
                splash.ShowDialog();
            }

            // Die GUI ist nur Steuer- und Anzeigeoberfläche. Volatile Runtime-Daten werden vom Service-Prozess verwaltet.
            Application.Run(new TK_Server());
        }

        /// <summary>
        /// Prüft, ob die EXE als GUI-loser Runtime-Prozess laufen soll.
        /// GUI-Start ohne Parameter bleibt unverändert.
        /// Einziger Service-Startweg: -startsvc.
        /// </summary>
        private static bool ShouldRunAsRuntimeProcess(string[] args) {
            if (args == null || args.Length == 0) {
                return false;
            }

            return IsStartSvcRequest(args);
        }

        /// <summary>
        /// Startet den TK-Server als separaten Runtime-Prozess ohne GUI.
        /// Dieser Prozess bleibt unabhängig von der GUI aktiv.
        /// </summary>
        private static void RunRuntimeProcess(string[] args) {
            RuntimeLoadOptions loadOptions = new() { AutoDetect = true };

            // Wird der Prozess vom Windows Service Control Manager gestartet, muss ServiceBase.Run verwendet werden.
            // Direktstart per Konsole/Verknüpfung mit -startsvc bleibt weiterhin als GUI-loser Runtime-Prozess möglich.
            if (!Environment.UserInteractive) {
                ServiceBase.Run(new TKWindowsService(loadOptions));
                return;
            }

            RunInteractiveRuntimeProcess(loadOptions);
        }

        /// <summary>
        /// Startet die Runtime ohne GUI, wenn die EXE manuell mit -startsvc gestartet wurde.
        /// </summary>
        private static void RunInteractiveRuntimeProcess(RuntimeLoadOptions loadOptions) {
            string controlDirectory = GetRuntimeControlDirectory();
            Directory.CreateDirectory(controlDirectory);

            string pidFile = Path.Combine(controlDirectory, "runtime.pid");
            string stopFile = Path.Combine(controlDirectory, "runtime.stop");

            if (File.Exists(stopFile)) {
                File.Delete(stopFile);
            }

            File.WriteAllText(pidFile, Environment.ProcessId.ToString());

            TKBase_LogManager runtimeLogger = new(null, Path.Combine(AppContext.BaseDirectory, "..", "log", "TK-Server.Service.log"), LogOutputMode.Service);
            runtimeLogger.TryConfigureFromDatabase();
            using TKBase_ServiceRunTime runtime = new(runtimeLogger);
            runtimeLogger.Info("TK-RuntimeProcess Start | Dauerhalter aktiv. DB-Sollzustand wird übernommen.");

            try {
                RuntimeLoadOptions? appliedOptions = RuntimeLoadOptions.None;
                bool lastDatabaseAvailable = false;

                while (!File.Exists(stopFile)) {
                    bool databaseAvailable = TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(runtimeLogger, "InteractiveServicePoll", false);

                    if (databaseAvailable && !lastDatabaseAvailable) {
                        runtimeLogger.TryConfigureFromDatabase();
                    }

                    lastDatabaseAvailable = databaseAvailable;

                    RuntimeLoadOptions targetOptions = databaseAvailable
                        ? TKBase_ServiceRunTime.ResolveDesiredStartOptions(new TKSQL_Commands(runtimeLogger), runtimeLogger).Normalize()
                        : RuntimeLoadOptions.None;

                    if (!targetOptions.SameRuntimeAs(appliedOptions)) {
                        runtime.ApplyRuntimeOptions(targetOptions, databaseAvailable ? "InteractiveServicePoll" : "SQL-Offline-Schutz");
                        appliedOptions = targetOptions;
                    }

                    Thread.Sleep(3000);
                }
            } finally {
                runtime.StopAll();

                try {
                    if (File.Exists(pidFile)) {
                        File.Delete(pidFile);
                    }

                    if (File.Exists(stopFile)) {
                        File.Delete(stopFile);
                    }
                } catch {
                    // Prozessende darf durch Cleanup nicht blockiert werden.
                }
            }
        }


        /// <summary>
        /// Erkennt den eindeutigen Service-Startparameter.
        /// </summary>
        private static bool IsStartSvcRequest(string[] args) {
            return args.Any(x => string.Equals(x, "-startsvc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x, "/startsvc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x, "--startsvc", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Liest Parameterwerte aus Formen wie -load all, /load all, --load all oder -load=all.
        /// </summary>
        private static string? GetArgumentValue(string[] args, string name) {
            if (args == null || args.Length == 0) {
                return null;
            }

            string shortKeyDash = "-" + name;
            string longKeyDash = "--" + name;
            string shortKeySlash = "/" + name;

            for (int i = 0; i < args.Length; i++) {
                string current = args[i];

                if (current.StartsWith(shortKeyDash + "=", StringComparison.OrdinalIgnoreCase)) {
                    return current[(shortKeyDash.Length + 1)..];
                }

                if (current.StartsWith(longKeyDash + "=", StringComparison.OrdinalIgnoreCase)) {
                    return current[(longKeyDash.Length + 1)..];
                }

                if (current.StartsWith(shortKeySlash + "=", StringComparison.OrdinalIgnoreCase)) {
                    return current[(shortKeySlash.Length + 1)..];
                }

                if (string.Equals(current, shortKeyDash, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(current, longKeyDash, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(current, shortKeySlash, StringComparison.OrdinalIgnoreCase)) {
                    if (i + 1 < args.Length) {
                        return args[i + 1];
                    }

                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Liefert den Ablageort fuer Runtime-Steuerdateien.
        /// </summary>
        internal static string GetRuntimeControlDirectory() {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "TK-Server");
        }

        /// <summary>
        /// The ResolveManagedAssembly
        /// </summary>
        /// <param name="context">The context<see cref="AssemblyLoadContext"/></param>
        /// <param name="name">The name<see cref="AssemblyName"/></param>
        /// <returns>The <see cref="Assembly?"/></returns>
        private static Assembly? ResolveManagedAssembly(AssemblyLoadContext context, AssemblyName name) {
            string assemblyFileName = (name.Name ?? string.Empty) + ".dll";
            foreach (string candidate in EnumerateManagedAssemblyCandidates(assemblyFileName)) {
                if (File.Exists(candidate)) {
                    return context.LoadFromAssemblyPath(candidate);
                }
            }

            return null;
        }

        /// <summary>
        /// The ResolveUnmanagedDll
        /// </summary>
        /// <param name="assembly">The assembly<see cref="Assembly"/></param>
        /// <param name="unmanagedDllName">The unmanagedDllName<see cref="string"/></param>
        /// <returns>The <see cref="IntPtr"/></returns>
        private static IntPtr ResolveUnmanagedDll(Assembly assembly, string unmanagedDllName) {
            foreach (string candidate in EnumerateNativeLibraryCandidates(unmanagedDllName)) {
                if (!File.Exists(candidate)) {
                    continue;
                }

                try {
                    return NativeLibrary.Load(candidate);
                } catch {
                    // Nächsten Kandidaten versuchen.
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// The EnumerateManagedAssemblyCandidates
        /// </summary>
        /// <param name="assemblyFileName">The assemblyFileName<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> EnumerateManagedAssemblyCandidates(string assemblyFileName) {
            string libPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "lib"));
            string runtimesPath = Path.Combine(libPath, "runtimes");

            if (Directory.Exists(runtimesPath)) {
                foreach (string candidate in Directory
                    .EnumerateFiles(runtimesPath, assemblyFileName, SearchOption.AllDirectories)
                    .OrderByDescending(IsPreferredWindowsRuntimePath)
                    .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)) {
                    yield return candidate;
                }
            }

            string directLibPath = Path.Combine(libPath, assemblyFileName);
            yield return directLibPath;
        }

        /// <summary>
        /// The EnumerateNativeLibraryCandidates
        /// </summary>
        /// <param name="unmanagedDllName">The unmanagedDllName<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> EnumerateNativeLibraryCandidates(string unmanagedDllName) {
            foreach (string root in GetNativeSearchRoots()) {
                if (!Directory.Exists(root)) {
                    continue;
                }

                foreach (string fileName in BuildNativeFileNames(unmanagedDllName)) {
                    foreach (string candidate in Directory
                        .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                        .OrderByDescending(IsPreferredWindowsRuntimePath)
                        .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)) {
                        yield return candidate;
                    }
                }
            }
        }

        /// <summary>
        /// The GetNativeSearchRoots
        /// </summary>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> GetNativeSearchRoots() {
            string libPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "lib"));
            yield return Path.Combine(libPath, "runtimes");
            yield return libPath;
        }

        /// <summary>
        /// The BuildNativeFileNames
        /// </summary>
        /// <param name="unmanagedDllName">The unmanagedDllName<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> BuildNativeFileNames(string unmanagedDllName) {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            void Add(string value) {
                if (!string.IsNullOrWhiteSpace(value)) {
                    names.Add(value);
                }
            }

            Add(unmanagedDllName);

            if (!unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                Add(unmanagedDllName + ".dll");
            }

            if (!unmanagedDllName.StartsWith("lib", StringComparison.OrdinalIgnoreCase)) {
                Add("lib" + unmanagedDllName);
                Add("lib" + unmanagedDllName + ".dll");
            } else if (!unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                Add(unmanagedDllName + ".dll");
            }

            return names;
        }

        /// <summary>
        /// The IsPreferredWindowsRuntimePath
        /// </summary>
        /// <param name="path">The path<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsPreferredWindowsRuntimePath(string path) {
            string normalized = path.Replace('/', '\\');
            if (normalized.Contains("\\win-x64\\", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (normalized.Contains("\\win\\", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return false;
        }
    }
}
