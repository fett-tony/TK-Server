namespace TKServer.SQL {
    using sipcryptlib;
    using Microsoft.Data.SqlClient;
    using System;
    using System.Data;
    using System.IO;
    using System.Xml.Linq;
    using TKServer.Properties;

    using TKServer.BASE;

    /// <summary>
    /// Defines the <see cref="TKSQL_ConnectionClass" />
    /// </summary>
    public class TKSQL_ConnectionClass {
        /// <summary>
        /// Defines the _connection
        /// </summary>
        private static SqlConnection? _connection;

        /// <summary>
        /// Synchronizes the cached database availability state.
        /// </summary>
        private static readonly object _availabilityLock = new object();

        /// <summary>
        /// Defines whether the last SQL availability check succeeded.
        /// </summary>
        private static bool _databaseAvailable;

        /// <summary>
        /// Defines whether a SQL availability check has already been executed.
        /// </summary>
        private static bool _databaseAvailabilityChecked;

        /// <summary>
        /// Defines the last SQL availability error.
        /// </summary>
        private static string _lastDatabaseAvailabilityError = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the last SQL availability check succeeded.
        /// </summary>
        public static bool DatabaseAvailable {
            get {
                lock (_availabilityLock) {
                    return _databaseAvailabilityChecked && _databaseAvailable;
                }
            }
        }

        /// <summary>
        /// Gets the last SQL availability error.
        /// </summary>
        public static string LastDatabaseAvailabilityError {
            get {
                lock (_availabilityLock) {
                    return _lastDatabaseAvailabilityError;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSQL_ConnectionClass"/> class.
        /// </summary>
        /// <param name="logMgmt">The logMgmt<see cref="TKBase_LogManager?"/></param>
        public TKSQL_ConnectionClass(TKBase_LogManager? logMgmt = null) {
        }

        /// <summary>
        /// Defines the TK-Server installation configuration folder used by GUI and service.
        /// </summary>
        private static readonly string MachineConfigFolder = ResolveInstallConfigFolder();

        /// <summary>
        /// Defines the shared SQL configuration file used by GUI and service.
        /// </summary>
        private static readonly string MachineSqlConfigPath = Path.Combine(MachineConfigFolder, "TKSQL_DB.config");

        /// <summary>
        /// Synchronizes access to the machine-wide SQL configuration file.
        /// </summary>
        private static readonly object _sqlConfigLock = new object();

        /// <summary>
        /// Represents the machine-wide SQL connection settings.
        /// </summary>
        public sealed class SqlConnectionSettings {
            /// <summary>Gets or sets the SQL server name or address.</summary>
            public string Server { get; set; } = string.Empty;

            /// <summary>Gets or sets the database name.</summary>
            public string Database { get; set; } = string.Empty;

            /// <summary>Gets or sets a value indicating whether Windows authentication is used.</summary>
            public bool IntegratedSecurity { get; set; }

            /// <summary>Gets or sets a value indicating whether SQL encryption is enabled.</summary>
            public bool Encrypt { get; set; }

            /// <summary>Gets or sets a value indicating whether the server certificate is trusted.</summary>
            public bool TrustServerCertificate { get; set; }

            /// <summary>Gets or sets the SQL login user name.</summary>
            public string UserID { get; set; } = string.Empty;

            /// <summary>Gets or sets the encrypted SQL login password.</summary>
            public string Password { get; set; } = string.Empty;

            /// <summary>Gets or sets the SQL server port.</summary>
            public int Port { get; set; }

            /// <summary>Gets or sets the connection builder type.</summary>
            public string ConBuilder { get; set; } = string.Empty;
        }

        /// <summary>
        /// Resolves the shared TK-Server configuration folder.
        /// </summary>
        /// <returns>The <see cref="string"/>.</returns>
        private static string ResolveInstallConfigFolder() {
            string baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            DirectoryInfo baseInfo = new DirectoryInfo(baseDirectory);

            if (string.Equals(baseInfo.Name, "bin", StringComparison.OrdinalIgnoreCase) &&
                baseInfo.Parent != null) {
                return Path.Combine(baseInfo.Parent.FullName, "conf");
            }

            return Path.Combine(baseDirectory, "conf");
        }

        /// <summary>
        /// Gets the active SQL settings for GUI display and service usage.
        /// </summary>
        /// <returns>The <see cref="SqlConnectionSettings"/>.</returns>
        public static SqlConnectionSettings LoadSqlConnectionSettings() {
            lock (_sqlConfigLock) {
                if (File.Exists(MachineSqlConfigPath)) {
                    return LoadMachineSqlConnectionSettings();
                }

                SqlConnectionSettings settings = LoadDefaultSqlConnectionSettings();
                SaveSqlConnectionSettings(settings);
                return settings;
            }
        }

        /// <summary>
        /// Saves SQL settings machine-wide so GUI and service use the same database connection.
        /// </summary>
        /// <param name="settings">The settings<see cref="SqlConnectionSettings"/>.</param>
        public static void SaveSqlConnectionSettings(SqlConnectionSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            lock (_sqlConfigLock) {
                Directory.CreateDirectory(MachineConfigFolder);

                XDocument document = new XDocument(
                    new XElement("TKSQL_DB",
                        new XElement("Server", settings.Server ?? string.Empty),
                        new XElement("Database", settings.Database ?? string.Empty),
                        new XElement("Integrated_Security", settings.IntegratedSecurity),
                        new XElement("Encrypt", settings.Encrypt),
                        new XElement("TrustServerCertificate", settings.TrustServerCertificate),
                        new XElement("UserID", settings.UserID ?? string.Empty),
                        new XElement("Password", settings.Password ?? string.Empty),
                        new XElement("Port", settings.Port),
                        new XElement("ConBuilder", settings.ConBuilder ?? string.Empty)));

                string tempPath = MachineSqlConfigPath + ".tmp";
                document.Save(tempPath);

                if (File.Exists(MachineSqlConfigPath)) {
                    File.Replace(tempPath, MachineSqlConfigPath, null);
                } else {
                    File.Move(tempPath, MachineSqlConfigPath);
                }

                try {
                    _connection?.Dispose();
                } catch {
                    // Verbindungswechsel darf das Speichern der Maschinenkonfiguration nicht blockieren.
                }

                _connection = null;
                SqlConnection.ClearAllPools();
            }
        }

        /// <summary>
        /// Gets the machine-wide SQL configuration file path.
        /// </summary>
        /// <returns>The <see cref="string"/>.</returns>
        public static string GetSqlConnectionSettingsPath() {
            return MachineSqlConfigPath;
        }

        /// <summary>
        /// The connectionString
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        public static string connectionString() {
            SqlConnectionSettings settings = LoadSqlConnectionSettings();
            string conString = string.Empty;

            switch (settings.ConBuilder) {
                case "LocalBuilder": {
                        var ConBuilder = new SqlConnectionStringBuilder {
                            DataSource = settings.Server,
                            InitialCatalog = settings.Database,
                            IntegratedSecurity = settings.IntegratedSecurity,
                            Encrypt = settings.Encrypt,
                            TrustServerCertificate = settings.TrustServerCertificate,
                            MultipleActiveResultSets = true
                        };
                        conString = ConBuilder.ConnectionString;
                        break;
                    }

                case "SQLLoginBuilder": {
                        var ConBuilder = new SqlConnectionStringBuilder {
                            DataSource = $"{settings.Server},{settings.Port}",
                            InitialCatalog = settings.Database,
                            UserID = settings.UserID,
                            Password = DecryptSqlPassword(settings.Password),
                            Encrypt = settings.Encrypt,
                            TrustServerCertificate = settings.TrustServerCertificate,
                            MultipleActiveResultSets = true
                        };
                        conString = ConBuilder.ConnectionString;
                        break;
                    }

                case "WinAuthBuilder": {
                        var ConBuilder = new SqlConnectionStringBuilder {
                            DataSource = $"{settings.Server},{settings.Port}",
                            InitialCatalog = settings.Database,
                            IntegratedSecurity = settings.IntegratedSecurity,
                            Encrypt = settings.Encrypt,
                            TrustServerCertificate = settings.TrustServerCertificate,
                            MultipleActiveResultSets = true
                        };
                        conString = ConBuilder.ConnectionString;
                        break;
                    }

                default:
                    throw new Exception("Unbekannter ConnectionBuilder-Typ.");
            }

            return conString;
        }

        /// <summary>
        /// Loads the default SQL settings from the generated settings file.
        /// </summary>
        /// <returns>The <see cref="SqlConnectionSettings"/>.</returns>
        private static SqlConnectionSettings LoadDefaultSqlConnectionSettings() {
            return new SqlConnectionSettings {
                Server = TKSQL_DB.Default.Server,
                Database = TKSQL_DB.Default.Database,
                IntegratedSecurity = TKSQL_DB.Default.Integrated_Security,
                Encrypt = TKSQL_DB.Default.Encrypt,
                TrustServerCertificate = TKSQL_DB.Default.TrustServerCertificate,
                UserID = TKSQL_DB.Default.UserID,
                Password = TKSQL_DB.Default.Password,
                Port = TKSQL_DB.Default.Port,
                ConBuilder = TKSQL_DB.Default.ConBuilder
            };
        }

        /// <summary>
        /// Loads SQL settings from the machine-wide configuration file.
        /// </summary>
        /// <returns>The <see cref="SqlConnectionSettings"/>.</returns>
        private static SqlConnectionSettings LoadMachineSqlConnectionSettings() {
            XDocument document = XDocument.Load(MachineSqlConfigPath);
            XElement root = document.Root ?? throw new InvalidOperationException("SQL-Konfigurationsdatei ist leer.");

            return new SqlConnectionSettings {
                Server = ReadString(root, "Server", TKSQL_DB.Default.Server),
                Database = ReadString(root, "Database", TKSQL_DB.Default.Database),
                IntegratedSecurity = ReadBool(root, "Integrated_Security", TKSQL_DB.Default.Integrated_Security),
                Encrypt = ReadBool(root, "Encrypt", TKSQL_DB.Default.Encrypt),
                TrustServerCertificate = ReadBool(root, "TrustServerCertificate", TKSQL_DB.Default.TrustServerCertificate),
                UserID = ReadString(root, "UserID", TKSQL_DB.Default.UserID),
                Password = ReadString(root, "Password", TKSQL_DB.Default.Password),
                Port = ReadInt(root, "Port", TKSQL_DB.Default.Port),
                ConBuilder = ReadString(root, "ConBuilder", TKSQL_DB.Default.ConBuilder)
            };
        }

        /// <summary>
        /// Reads a string element from the SQL configuration.
        /// </summary>
        private static string ReadString(XElement root, string name, string fallback) {
            string? value = root.Element(name)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        /// <summary>
        /// Reads a Boolean element from the SQL configuration.
        /// </summary>
        private static bool ReadBool(XElement root, string name, bool fallback) {
            string? value = root.Element(name)?.Value;
            return bool.TryParse(value, out bool parsed) ? parsed : fallback;
        }

        /// <summary>
        /// Reads an integer element from the SQL configuration.
        /// </summary>
        private static int ReadInt(XElement root, string name, int fallback) {
            string? value = root.Element(name)?.Value;
            return int.TryParse(value, out int parsed) ? parsed : fallback;
        }

        /// <summary>
        /// Decrypts the stored SQL password if possible.
        /// </summary>
        private static string DecryptSqlPassword(string password) {
            if (string.IsNullOrEmpty(password)) {
                return string.Empty;
            }

            try {
                return SipCryptor.Decrypt(password);
            } catch {
                return password;
            }
        }

        /// <summary>
        /// Checks whether the SQL database can be reached without starting database-dependent tasks.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="operation">The operation<see cref="string?"/></param>
        /// <param name="forceLog">The forceLog<see cref="bool"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool TryEnsureDatabaseAvailable(TKBase_LogManager? logger = null, string? operation = null, bool forceLog = false) {
            bool wasAvailable;

            lock (_availabilityLock) {
                wasAvailable = _databaseAvailabilityChecked && _databaseAvailable;
            }

            try {
                using SqlConnection con = new SqlConnection(BuildAvailabilityConnectionString());
                con.Open();
                SetDatabaseAvailable();

                if (!wasAvailable) {
                    logger?.Info($"SQL-Datenbank erreichbar | Aufgabe={NormalizeOperation(operation)} | DB-abhängige Aktionen werden wieder freigegeben.");
                }

                return true;
            } catch (Exception ex) {
                SetDatabaseUnavailable(ex.Message);

                try {
                    _connection?.Dispose();
                } catch {
                    // Absichtlich ignorieren: Fehler beim Freigeben duerfen den DB-Guard nicht blockieren.
                }

                _connection = null;

                try {
                    SqlConnection.ClearAllPools();
                } catch {
                    // Pool-Cleanup darf den Verfügbarkeitscheck nicht blockieren.
                }

                if (forceLog || wasAvailable) {
                    logger?.Error($"SQL-Datenbank nicht erreichbar | Aufgabe={NormalizeOperation(operation)} | DB-abhängige Aktionen werden nicht gestartet | Fehler={ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// Builds a short-timeout connection string used only for recurring availability checks.
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildAvailabilityConnectionString() {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString()) {
                ConnectTimeout = 2
            };

            return builder.ConnectionString;
        }

        /// <summary>
        /// Marks the cached SQL availability state as available.
        /// </summary>
        private static void SetDatabaseAvailable() {
            lock (_availabilityLock) {
                _databaseAvailabilityChecked = true;
                _databaseAvailable = true;
                _lastDatabaseAvailabilityError = string.Empty;
            }
        }

        /// <summary>
        /// Marks the cached SQL availability state as unavailable.
        /// </summary>
        /// <param name="error">The error<see cref="string"/></param>
        private static void SetDatabaseUnavailable(string error) {
            lock (_availabilityLock) {
                _databaseAvailabilityChecked = true;
                _databaseAvailable = false;
                _lastDatabaseAvailabilityError = error ?? string.Empty;
            }
        }

        /// <summary>
        /// Normalizes the operation name used in DB availability messages.
        /// </summary>
        /// <param name="operation">The operation<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeOperation(string? operation) {
            return string.IsNullOrWhiteSpace(operation) ? "unbekannt" : operation.Trim();
        }

        /// <summary>
        /// The GetConnection
        /// </summary>
        /// <returns>The <see cref="SqlConnection"/></returns>
        public static SqlConnection GetConnection() {
            try {
                if (_connection == null) {
                    _connection = new SqlConnection(connectionString());
                }

                if (_connection.State == ConnectionState.Broken) {
                    _connection.Close();
                    _connection.Open();
                } else if (_connection.State == ConnectionState.Closed) {
                    _connection.Open();
                }

                SetDatabaseAvailable();
                return _connection;
            } catch (Exception ex) {
                SetDatabaseUnavailable(ex.Message);

                try {
                    _connection?.Dispose();
                } catch {
                    // Absichtlich ignorieren: der ursprüngliche SQL-Fehler ist maßgeblich.
                }

                _connection = null;
                throw;
            }
        }

        /// <summary>
        /// Erstellt eine neue geöffnete SQL-Verbindung für lokale Transaktionen oder isolierte Adapterzugriffe.
        /// </summary>
        /// <returns>The <see cref="SqlConnection"/></returns>
        public static SqlConnection CreateOpenConnection() {
            try {
                SqlConnection con = new SqlConnection(connectionString());
                con.Open();
                SetDatabaseAvailable();
                return con;
            } catch (Exception ex) {
                SetDatabaseUnavailable(ex.Message);
                throw;
            }
        }


        /// <summary>
        /// Clears the cached SQL connection so the next access uses the current machine-wide configuration.
        /// </summary>
        public static void ResetConnectionCache() {
            try {
                _connection?.Dispose();
            } catch {
                // Cache-Reset darf den aufrufenden Runtime-Pfad nicht blockieren.
            }

            _connection = null;

            try {
                SqlConnection.ClearAllPools();
            } catch {
                // Pool-Cleanup ist best effort.
            }
        }

        /// <summary>
        /// The CloseConnection
        /// </summary>
        public static void CloseConnection() {
            if (_connection != null &&
                _connection.State != ConnectionState.Closed) {
                _connection.Close();
            }
        }

        /// <summary>
        /// The GetDataTable
        /// </summary>
        /// <param name="query">The query<see cref="string"/></param>
        /// <returns>The <see cref="DataTable"/></returns>
        public static DataTable GetDataTable(string query) {
            try {
                using SqlConnection con = new SqlConnection(connectionString());
                using SqlCommand cmd = new SqlCommand(query, con);
                using SqlDataAdapter adapter = new SqlDataAdapter(cmd);

                DataTable table = new DataTable();
                con.Open();
                SetDatabaseAvailable();
                adapter.Fill(table);
                return table;
            } catch (Exception ex) {
                SetDatabaseUnavailable(ex.Message);
                throw;
            }
        }
    }
}
