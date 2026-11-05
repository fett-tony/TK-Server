namespace TKServer.SQL {
    using Microsoft.Data.SqlClient;
    using sipcryptlib;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text.RegularExpressions;

    using TKServer.BASE;
    using TKServer.SipExt;
    using TKServer.SVC;

    /// <summary>
    /// Defines the <see cref="TKSQL_Commands" />
    /// </summary>
    public sealed class TKSQL_Commands {
        /// <summary>
        /// Defines the _sqlConnection
        /// </summary>
        private readonly TKSQL_ConnectionClass _sqlConnection;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSQL_Commands"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSQL_Commands(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sqlConnection = new TKSQL_ConnectionClass(logger);
        }

        /// <summary>
        /// The LoadTkPropertiesTable
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadTkPropertiesTable() {
            const string query = @"
SELECT *
FROM dbo.TK_Properties
ORDER BY Id ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// Prueft, ob die manuell einzuspielende Runtime-Steuertabelle vorhanden und befuellt ist.
        /// Es werden bewusst keine Schemaaenderungen automatisch ausgefuehrt.
        /// </summary>
        private void ValidateRuntimeDesiredStateColumns() {
            const string query = @"
DECLARE @SchemaReady bit = 0;

IF OBJECT_ID(N'dbo.TK_RuntimeAreaControl', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.TK_RuntimeAreaControl', 'AreaKey') IS NOT NULL
   AND COL_LENGTH('dbo.TK_RuntimeAreaControl', 'DesiredEnabled') IS NOT NULL
   AND COL_LENGTH('dbo.TK_RuntimeAreaControl', 'ActualState') IS NOT NULL
BEGIN
    EXEC sp_executesql
        N'SELECT @SchemaReadyOut = CASE
            WHEN EXISTS (SELECT 1 FROM dbo.TK_RuntimeAreaControl WHERE AreaKey = N''SIPInt'')
             AND EXISTS (SELECT 1 FROM dbo.TK_RuntimeAreaControl WHERE AreaKey = N''Provider'')
             AND EXISTS (SELECT 1 FROM dbo.TK_RuntimeAreaControl WHERE AreaKey = N''GSM'')
            THEN 1 ELSE 0 END;',
        N'@SchemaReadyOut bit OUTPUT',
        @SchemaReadyOut = @SchemaReady OUTPUT;
END;

SELECT @SchemaReady AS SchemaReady;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            DataRow? row = ExecuteSingleRow(cmd);
            bool schemaReady = row != null && Convert.ToInt32(row["SchemaReady"]) == 1;

            if (!schemaReady) {
                throw new InvalidOperationException("Runtime-Steuertabelle dbo.TK_RuntimeAreaControl fehlt oder ist nicht vollstaendig befuellt. Erwartet werden SIPInt, Provider und GSM.");
            }
        }

        /// <summary>
        /// Liest den gespeicherten Runtime-Sollzustand aus TK_RuntimeAreaControl.
        /// Die GUI setzt DesiredEnabled, der Service fuehrt ihn aus.
        /// </summary>
        public (bool SipRuntimeEnabled, bool ProviderRegistrationEnabled, bool GsmRuntimeEnabled) GetRuntimeDesiredState() {
            ValidateRuntimeDesiredStateColumns();

            const string query = @"
SELECT
    AreaKey,
    DesiredEnabled
FROM dbo.TK_RuntimeAreaControl
WHERE AreaKey IN (N'SIPInt', N'Provider', N'GSM')
ORDER BY AreaKey ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            List<DataRow> rows = ExecuteRows(cmd);

            bool sipRuntimeEnabled = rows.Any(row => string.Equals(Convert.ToString(row["AreaKey"]), "SIPInt", StringComparison.OrdinalIgnoreCase)
                && row["DesiredEnabled"] != DBNull.Value
                && Convert.ToBoolean(row["DesiredEnabled"]));

            bool providerRegistrationEnabled = rows.Any(row => string.Equals(Convert.ToString(row["AreaKey"]), "Provider", StringComparison.OrdinalIgnoreCase)
                && row["DesiredEnabled"] != DBNull.Value
                && Convert.ToBoolean(row["DesiredEnabled"]));

            bool gsmRuntimeEnabled = rows.Any(row => string.Equals(Convert.ToString(row["AreaKey"]), "GSM", StringComparison.OrdinalIgnoreCase)
                && row["DesiredEnabled"] != DBNull.Value
                && Convert.ToBoolean(row["DesiredEnabled"]));

            return (sipRuntimeEnabled, providerRegistrationEnabled, gsmRuntimeEnabled);
        }

        /// <summary>
        /// Liest den aktuellen Soll-/Istzustand aller Runtime-Bereiche.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadRuntimeAreaControlTable() {
            ValidateRuntimeDesiredStateColumns();

            const string query = @"
SELECT
    AreaKey,
    DesiredEnabled,
    ActualState,
    LastCommandAt,
    LastAppliedAt,
    LastStartAt,
    LastStopAt,
    LastError,
    UpdatedAt
FROM dbo.TK_RuntimeAreaControl
WHERE AreaKey IN (N'SIPInt', N'Provider', N'GSM')
ORDER BY
    CASE AreaKey
        WHEN N'SIPInt' THEN 1
        WHEN N'Provider' THEN 2
        WHEN N'GSM' THEN 3
        ELSE 99
    END;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// Setzt den zentralen Runtime-Sollzustand in TK_RuntimeAreaControl.
        /// Null-Werte lassen den bestehenden Wert unverÃƒÂ¤ndert.
        /// </summary>
        public int SetRuntimeDesiredState(bool? sipRuntimeEnabled = null, bool? providerRegistrationEnabled = null, bool? gsmRuntimeEnabled = null) {
            ValidateRuntimeDesiredStateColumns();

            if (sipRuntimeEnabled == null
                && providerRegistrationEnabled == null
                && gsmRuntimeEnabled == null) {
                return 0;
            }

            const string query = @"
UPDATE dbo.TK_RuntimeAreaControl
SET
    DesiredEnabled = CASE
        WHEN AreaKey = N'SIPInt' AND @SipRuntimeEnabled IS NOT NULL THEN @SipRuntimeEnabled
        WHEN AreaKey = N'Provider' AND @ProviderRegistrationEnabled IS NOT NULL THEN @ProviderRegistrationEnabled
        WHEN AreaKey = N'GSM' AND @GsmRuntimeEnabled IS NOT NULL THEN @GsmRuntimeEnabled
        ELSE DesiredEnabled
    END,
    LastCommandAt = CASE
        WHEN AreaKey = N'SIPInt' AND @SipRuntimeEnabled IS NOT NULL THEN SYSDATETIME()
        WHEN AreaKey = N'Provider' AND @ProviderRegistrationEnabled IS NOT NULL THEN SYSDATETIME()
        WHEN AreaKey = N'GSM' AND @GsmRuntimeEnabled IS NOT NULL THEN SYSDATETIME()
        ELSE LastCommandAt
    END,
    UpdatedAt = CASE
        WHEN AreaKey = N'SIPInt' AND @SipRuntimeEnabled IS NOT NULL THEN SYSDATETIME()
        WHEN AreaKey = N'Provider' AND @ProviderRegistrationEnabled IS NOT NULL THEN SYSDATETIME()
        WHEN AreaKey = N'GSM' AND @GsmRuntimeEnabled IS NOT NULL THEN SYSDATETIME()
        ELSE UpdatedAt
    END
WHERE
    (AreaKey = N'SIPInt' AND @SipRuntimeEnabled IS NOT NULL)
 OR (AreaKey = N'Provider' AND @ProviderRegistrationEnabled IS NOT NULL)
 OR (AreaKey = N'GSM' AND @GsmRuntimeEnabled IS NOT NULL);";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.Add("@SipRuntimeEnabled", SqlDbType.Bit).Value = (object?)sipRuntimeEnabled ?? DBNull.Value;
            cmd.Parameters.Add("@ProviderRegistrationEnabled", SqlDbType.Bit).Value = (object?)providerRegistrationEnabled ?? DBNull.Value;
            cmd.Parameters.Add("@GsmRuntimeEnabled", SqlDbType.Bit).Value = (object?)gsmRuntimeEnabled ?? DBNull.Value;
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Setzt den Istzustand eines Runtime-Bereichs. Der Service schreibt diese Werte.
        /// </summary>
        public int SetRuntimeAreaActualState(string areaKey, string actualState, string? lastError = null) {
            ValidateRuntimeDesiredStateColumns();

            string normalizedAreaKey = NormalizeRuntimeAreaKey(areaKey);
            string normalizedActualState = NormalizeRuntimeActualState(actualState);
            string? normalizedLastError = string.IsNullOrWhiteSpace(lastError)
                ? null
                : lastError.Length > 500 ? lastError.Substring(0, 500) : lastError;

            const string query = @"
UPDATE dbo.TK_RuntimeAreaControl
SET
    ActualState = @ActualState,
    LastAppliedAt = SYSDATETIME(),
    LastStartAt = CASE
        WHEN @ActualState = N'Starting' THEN SYSDATETIME()
        ELSE LastStartAt
    END,
    LastStopAt = CASE
        WHEN @ActualState = N'Stopped' THEN SYSDATETIME()
        ELSE LastStopAt
    END,
    LastError = CASE
        WHEN @ActualState = N'Error' THEN @LastError
        ELSE NULL
    END,
    UpdatedAt = SYSDATETIME()
WHERE AreaKey = @AreaKey;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.Add("@AreaKey", SqlDbType.NVarChar, 30).Value = normalizedAreaKey;
            cmd.Parameters.Add("@ActualState", SqlDbType.NVarChar, 30).Value = normalizedActualState;
            cmd.Parameters.Add("@LastError", SqlDbType.NVarChar, 500).Value = (object?)normalizedLastError ?? DBNull.Value;
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Normalisiert Runtime-Bereichsnamen fuer dbo.TK_RuntimeAreaControl.
        /// </summary>
        private static string NormalizeRuntimeAreaKey(string areaKey) {
            return areaKey?.Trim().ToUpperInvariant() switch {
                "SIPINT" => "SIPInt",
                "PROVIDER" => "Provider",
                "GSM" => "GSM",
                _ => throw new ArgumentException($"Unbekannter Runtime-Bereich: {areaKey}", nameof(areaKey))
            };
        }

        /// <summary>
        /// Normalisiert Runtime-Istzustaende fuer dbo.TK_RuntimeAreaControl.
        /// </summary>
        private static string NormalizeRuntimeActualState(string actualState) {
            return actualState?.Trim().ToUpperInvariant() switch {
                "STOPPED" => "Stopped",
                "STARTING" => "Starting",
                "RUNNING" => "Running",
                "STOPPING" => "Stopping",
                "ERROR" => "Error",
                _ => throw new ArgumentException($"Unbekannter Runtime-Istzustand: {actualState}", nameof(actualState))
            };
        }

        /// <summary>
        /// The LoadAudioCodecSelectionTable
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadAudioCodecSelectionTable() {
            const string query = @"
SELECT
    ID,
    CodecKey,
    DisplayName,
    SupportsSip,
    SupportsRtp,
    SupportsGsmBridge,
    Active
FROM dbo.TK_AudioCodec
ORDER BY
    Active DESC,
    SupportsGsmBridge DESC,
    CodecKey ASC,
    ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// The UpdateTkPropertiesCodec
        /// </summary>
        /// <param name="propertiesId">The propertiesId<see cref="int"/></param>
        /// <param name="codecColumnName">The codecColumnName<see cref="string"/></param>
        /// <param name="codecId">The codecId<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateTkPropertiesCodec(int propertiesId, string codecColumnName, int codecId) {
            string columnName = codecColumnName switch {
                "SIPCodec" => "SIPCodec",
                "GSMCodec" => "GSMCodec",
                _ => throw new ArgumentException($"Nicht erlaubte Codec-Spalte: {codecColumnName}", nameof(codecColumnName))
            };

            using SqlCommand cmd = new SqlCommand($@"
UPDATE dbo.TK_Properties
SET {columnName} = @CodecID,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@CodecID", codecId);
            cmd.Parameters.AddWithValue("@Id", propertiesId);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The UpdateTkProperties
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateTkProperties(DataRow row) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_Properties
SET
    PLZ = @PLZ,
    City = @City,
    Country = @Country,
    CountryID = @CountryID,
    AreaID = @AreaID,
    ServerIP = @ServerIP,
    SipProxy = @SipProxy,
    Registrar = @Registrar,
    Realm = @Realm,
    StunServer = @StunServer,
    OutboundProxy = @OutboundProxy,
    SipTransport = @SipTransport,
    SipPort = @SipPort,
    UseSRTP = @UseSRTP,
    RtpPortStart = @RtpPortStart,
    RtpPortEnd = @RtpPortEnd,
    GSMAudioPort = @GSMAudioPort,
    SIPCodec = @SIPCodec,
    GSMCodec = @GSMCodec,
    RegisterExpire = @RegisterExpire,
    UseAdsProfileMediaPath = @UseAdsProfileMediaPath,
    UserMediaRootPath = @UserMediaRootPath,
    UserMediaFolderName = @UserMediaFolderName,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Id", DbIntRequired(row, "Id"));
            cmd.Parameters.AddWithValue("@PLZ", DbInt(row, "PLZ"));
            cmd.Parameters.AddWithValue("@City", DbString(row, "City"));
            cmd.Parameters.AddWithValue("@Country", DbString(row, "Country"));
            cmd.Parameters.AddWithValue("@CountryID", DbInt(row, "CountryID"));
            cmd.Parameters.AddWithValue("@AreaID", DbInt(row, "AreaID"));
            cmd.Parameters.AddWithValue("@ServerIP", DbString(row, "ServerIP"));
            cmd.Parameters.AddWithValue("@SipProxy", DbString(row, "SipProxy"));
            cmd.Parameters.AddWithValue("@Registrar", DbString(row, "Registrar"));
            cmd.Parameters.AddWithValue("@Realm", DbString(row, "Realm"));
            cmd.Parameters.AddWithValue("@StunServer", DbString(row, "StunServer"));
            cmd.Parameters.AddWithValue("@OutboundProxy", DbString(row, "OutboundProxy"));
            cmd.Parameters.AddWithValue("@SipTransport", DbString(row, "SipTransport"));
            cmd.Parameters.AddWithValue("@SipPort", DbInt(row, "SipPort"));
            cmd.Parameters.AddWithValue("@UseSRTP", DbBool(row, "UseSRTP"));
            cmd.Parameters.AddWithValue("@RtpPortStart", DbInt(row, "RtpPortStart"));
            cmd.Parameters.AddWithValue("@RtpPortEnd", DbInt(row, "RtpPortEnd"));
            cmd.Parameters.AddWithValue("@GSMAudioPort", DbInt(row, "GSMAudioPort"));
            cmd.Parameters.AddWithValue("@SIPCodec", DbInt(row, "SIPCodec"));
            cmd.Parameters.AddWithValue("@GSMCodec", DbInt(row, "GSMCodec"));
            cmd.Parameters.AddWithValue("@RegisterExpire", DbInt(row, "RegisterExpire"));
            cmd.Parameters.AddWithValue("@UseAdsProfileMediaPath", DbBool(row, "UseAdsProfileMediaPath"));
            cmd.Parameters.AddWithValue("@UserMediaRootPath", DbString(row, "UserMediaRootPath"));
            cmd.Parameters.AddWithValue("@UserMediaFolderName", DbRequiredString(row, "UserMediaFolderName", "User"));

            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The UpdateTkPropertiesAudioFolderSettings
        /// </summary>
        /// <param name="propertiesId">The propertiesId<see cref="int"/></param>
        /// <param name="useAdsProfileMediaPath">The useAdsProfileMediaPath<see cref="bool"/></param>
        /// <param name="userMediaRootPath">The userMediaRootPath<see cref="string?"/></param>
        /// <param name="userMediaFolderName">The userMediaFolderName<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateTkPropertiesAudioFolderSettings(int propertiesId, bool useAdsProfileMediaPath, string? userMediaRootPath, string userMediaFolderName) {
            string folderName = string.IsNullOrWhiteSpace(userMediaFolderName) ? "User" : userMediaFolderName.Trim();

            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_Properties
SET
    UseAdsProfileMediaPath = @UseAdsProfileMediaPath,
    UserMediaRootPath = @UserMediaRootPath,
    UserMediaFolderName = @UserMediaFolderName,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Id", propertiesId);
            cmd.Parameters.AddWithValue("@UseAdsProfileMediaPath", useAdsProfileMediaPath);
            cmd.Parameters.AddWithValue("@UserMediaRootPath", string.IsNullOrWhiteSpace(userMediaRootPath) ? (object)DBNull.Value : userMediaRootPath.Trim());
            cmd.Parameters.AddWithValue("@UserMediaFolderName", folderName);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Prueft, ob die manuell einzuspielenden Logging-Spalten vorhanden sind.
        /// Es werden bewusst keine Schemaaenderungen automatisch ausgefuehrt.
        /// </summary>
        private void ValidateLoggingSettingsColumns() {
            const string query = @"
SELECT
    CASE
        WHEN COL_LENGTH('dbo.TK_Properties', 'LogLevel') IS NOT NULL
         AND COL_LENGTH('dbo.TK_Properties', 'DebugLoggingEnabled') IS NOT NULL
        THEN 1
        ELSE 0
    END AS SchemaReady;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            DataRow? row = ExecuteSingleRow(cmd);
            bool schemaReady = row != null && Convert.ToInt32(row["SchemaReady"]) == 1;

            if (!schemaReady) {
                throw new InvalidOperationException("Logging-Spalten fehlen in dbo.TK_Properties. Bitte zuerst die SQL-Datei TK_Properties_LoggingSettings_AddColumns_V0001.sql manuell ausfuehren.");
            }
        }

        /// <summary>
        /// Liest die zentralen Logging-Einstellungen aus TK_Properties.
        /// </summary>
        /// <returns>LogLevel und DebugLoggingEnabled.</returns>
        public (string LogLevel, bool DebugLoggingEnabled) GetLoggingSettings() {
            ValidateLoggingSettingsColumns();

            const string query = @"
SELECT TOP 1
    LogLevel,
    DebugLoggingEnabled
FROM dbo.TK_Properties
ORDER BY Id DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            DataRow? row = ExecuteSingleRow(cmd);
            if (row == null) {
                return ("Info", false);
            }

            string logLevel = NormalizeLogLevelForSql(Convert.ToString(row["LogLevel"]));
            bool debugLoggingEnabled = row["DebugLoggingEnabled"] != DBNull.Value
                && Convert.ToBoolean(row["DebugLoggingEnabled"]);

            return (logLevel, debugLoggingEnabled);
        }

        /// <summary>
        /// Speichert die zentralen Logging-Einstellungen in TK_Properties.
        /// </summary>
        /// <param name="propertiesId">The propertiesId<see cref="int"/></param>
        /// <param name="logLevel">The logLevel<see cref="string"/></param>
        /// <param name="debugLoggingEnabled">The debugLoggingEnabled<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateTkPropertiesLoggingSettings(int propertiesId, string logLevel, bool debugLoggingEnabled) {
            string normalizedLogLevel = NormalizeLogLevelForSql(logLevel);

            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_Properties
SET
    LogLevel = @LogLevel,
    DebugLoggingEnabled = @DebugLoggingEnabled,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Id", propertiesId);
            cmd.Parameters.AddWithValue("@LogLevel", normalizedLogLevel);
            cmd.Parameters.AddWithValue("@DebugLoggingEnabled", debugLoggingEnabled);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Normalisiert den LogLevel fuer SQL auf die erlaubten Werte Error, Warn oder Info.
        /// </summary>
        /// <param name="logLevel">The logLevel<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeLogLevelForSql(string? logLevel) {
            string value = (logLevel ?? string.Empty).Trim();
            return value.Equals("Error", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : value.Equals("Warn", StringComparison.OrdinalIgnoreCase)
                    ? "Warn"
                    : "Info";
        }

        /// <summary>
        /// The UpdateTkPropertiesCountrySettings
        /// </summary>
        /// <param name="propertiesId">The propertiesId<see cref="int"/></param>
        /// <param name="plz">The plz<see cref="int?"/></param>
        /// <param name="city">The city<see cref="string?"/></param>
        /// <param name="country">The country<see cref="string?"/></param>
        /// <param name="countryId">The countryId<see cref="int?"/></param>
        /// <param name="areaId">The areaId<see cref="int?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateTkPropertiesCountrySettings(int propertiesId, int? plz, string? city, string? country, int? countryId, int? areaId) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_Properties
SET
    PLZ = @PLZ,
    City = @City,
    Country = @Country,
    CountryID = @CountryID,
    AreaID = @AreaID,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Id", propertiesId);
            cmd.Parameters.AddWithValue("@PLZ", (object?)plz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", string.IsNullOrWhiteSpace(city) ? (object)DBNull.Value : city.Trim());
            cmd.Parameters.AddWithValue("@Country", string.IsNullOrWhiteSpace(country) ? (object)DBNull.Value : country.Trim());
            cmd.Parameters.AddWithValue("@CountryID", (object?)countryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AreaID", (object?)areaId ?? DBNull.Value);

            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The ResolveCountryPrefix
        /// </summary>
        /// <param name="countryInput">The countryInput<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? ResolveCountryPrefix(string countryInput) {
            string input = (countryInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(input)) return null;

            string digits = OnlyDigits(input);
            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    ID,
    ISO2,
    CountryName,
    PrefixDisplay,
    PrefixDigits
FROM dbo.TK_CountryPrefix
WHERE Active = 1
  AND (
       CountryName = @Input
       OR ISO2 = @Input
       OR ISO3 = @Input
       OR PrefixDisplay = @Input
       OR PrefixDigits = @Digits
       OR CountryCodeDigits = @Digits
       OR (ISO2 = N'DE' AND @Input IN (N'Deutschland', N'Germany', N'BRD'))
  )
ORDER BY SortOrder ASC, ID ASC;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Input", input);
            cmd.Parameters.AddWithValue("@Digits", string.IsNullOrWhiteSpace(digits) ? (object)DBNull.Value : digits);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The ResolveDeAreaCode
        /// </summary>
        /// <param name="cityInput">The cityInput<see cref="string"/></param>
        /// <param name="countryIso2">The countryIso2<see cref="string?"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? ResolveDeAreaCode(string cityInput, string? countryIso2) {
            string input = (cityInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(input)) return null;

            string digits = OnlyDigits(input);
            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    ID,
    AreaName,
    AreaCodeDigits,
    NationalPrefixDisplay,
    InternationalPrefixDisplay
FROM dbo.TK_DEAreaCode
WHERE Active = 1
  AND (@CountryISO2 IS NULL OR CountryISO2 = @CountryISO2)
  AND (
       AreaName = @Input
       OR AreaCodeDigits = @Digits
       OR NationalPrefixDisplay = @Input
       OR InternationalPrefixDisplay = @Input
       OR InternationalPrefixDigits = @Digits
  )
ORDER BY SortOrder ASC, ID ASC;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Input", input);
            cmd.Parameters.AddWithValue("@Digits", string.IsNullOrWhiteSpace(digits) ? (object)DBNull.Value : digits);
            cmd.Parameters.AddWithValue("@CountryISO2", string.IsNullOrWhiteSpace(countryIso2) ? (object)DBNull.Value : countryIso2);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetCountryPrefixById
        /// </summary>
        /// <param name="countryId">The countryId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetCountryPrefixById(int countryId) {
            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    ID,
    ISO2,
    CountryName,
    PrefixDisplay,
    PrefixDigits
FROM dbo.TK_CountryPrefix
WHERE ID = @ID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@ID", countryId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetDeAreaCodeById
        /// </summary>
        /// <param name="areaId">The areaId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetDeAreaCodeById(int areaId) {
            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    ID,
    AreaName,
    AreaCodeDigits,
    NationalPrefixDisplay,
    InternationalPrefixDisplay
FROM dbo.TK_DEAreaCode
WHERE ID = @ID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@ID", areaId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetActiveAudioCodecs
        /// </summary>
        /// <returns>The <see cref="List{TKBase_AudioCodecConfig}"/></returns>
        internal List<TKBase_AudioCodecConfig> GetActiveAudioCodecs() {
            const string query = @"
SELECT
    ID,
    CodecKey,
    DisplayName,
    Description,
    MediaType,
    RtpPayloadType,
    ClockRate,
    RtpClockRate,
    Channels,
    Bitrate,
    RtpEncodingName,
    RtpFormatParams,
    SampleFormat,
    FrameDurationMs,
    PtimeMs,
    MaxPtimeMs,
    CanDecode,
    CanEncode,
    IsPassThrough,
    IsDefault,
    IsPreferred,
    Active,
    SupportsSip,
    SupportsRtp,
    SupportsGsmBridge,
    SupportsInternal,
    SupportsExternal,
    SortOrder,
    JsonConfig
FROM dbo.TK_AudioCodec
WHERE Active = 1
  AND MediaType = 'Audio'
ORDER BY ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteList(cmd, TKBase_AudioCodecConfig.FromDataRow);
        }

        /// <summary>
        /// The GetActiveServiceLegs
        /// </summary>
        /// <returns>The <see cref="List{TKSVC_Session}"/></returns>
        public List<TKSVC_Session> GetActiveServiceLegs() {
            const string query = @"
SELECT
    *
FROM dbo.TK_SIP_ServiceLeg
WHERE Active = 1
ORDER BY SortOrder ASC, InternalNumber ASC, ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteList(cmd, TKSVC_Session.FromDataRow);
        }

        /// <summary>
        /// The GetActiveEmergencyRuleByNumber
        /// </summary>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetActiveEmergencyRuleByNumber(string number) {
            string raw = (number ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) {
                return null;
            }

            string digits = OnlyDigits(raw);
            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    ID,
    Notrufnummer,
    Bezeichnung,
    TargetType,
    TargetValue,
    ProviderAccountID,
    Priority,
    Active,
    CreatedAt,
    UpdatedAt
FROM dbo.TK_Notruf
WHERE Active = 1
  AND (
        LTRIM(RTRIM(Notrufnummer)) = @RawNumber
        OR (@Digits <> N'' AND LTRIM(RTRIM(Notrufnummer)) = @Digits)
      )
ORDER BY Priority ASC, ID ASC;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@RawNumber", raw);
            cmd.Parameters.AddWithValue("@Digits", digits);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetActiveServiceLegByInternalNumber
        /// </summary>
        /// <param name="internalNumber">The internalNumber<see cref="string"/></param>
        /// <returns>The <see cref="TKSVC_Session?"/></returns>
        public TKSVC_Session? GetActiveServiceLegByInternalNumber(string internalNumber) {
            const string query = @"
SELECT TOP 1
    *
FROM dbo.TK_SIP_ServiceLeg
WHERE InternalNumber = @InternalNumber
  AND Active = 1
ORDER BY SortOrder ASC, ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@InternalNumber", internalNumber ?? string.Empty);

            DataRow? row = ExecuteSingleRow(cmd);
            return row == null ? null : TKSVC_Session.FromDataRow(row);
        }

        /// <summary>
        /// The EnsureCallRecordingToggleServiceLeg
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int EnsureCallRecordingToggleServiceLeg() {
            const string query = @"
IF EXISTS (
    SELECT 1
    FROM dbo.TK_SIP_ServiceLeg
    WHERE ServiceCode = @ServiceCode
       OR InternalNumber = @InternalNumber
)
BEGIN
    UPDATE dbo.TK_SIP_ServiceLeg
    SET
        InternalNumber = @InternalNumber,
        DisplayName = @DisplayName,
        Description = @Description,
        ServiceCode = @ServiceCode,
        ServiceType = @ServiceType,
        PromptFile = @PromptFile,
        RecordTemplate = @RecordTemplate,
        JsonConfig = @JsonConfig,
        AutoAnswer = 1,
        KeepSessionUntilPeerEnded = 1,
        Active = 1,
        SortOrder = @SortOrder,
        MenuRootNumber = @MenuRootNumber,
        MenuOption = @MenuOption,
        MenuParentCode = @MenuParentCode,
        MenuSortOrder = @MenuSortOrder,
        IsMenuEntry = 1,
        IsDirectDialAllowed = 1,
        UpdatedAt = SYSDATETIME()
    WHERE ServiceCode = @ServiceCode
       OR InternalNumber = @InternalNumber;
END
ELSE
BEGIN
    INSERT INTO dbo.TK_SIP_ServiceLeg
    (
        InternalNumber,
        DisplayName,
        Description,
        ServiceCode,
        ServiceType,
        PromptFile,
        RecordTemplate,
        JsonConfig,
        AutoAnswer,
        KeepSessionUntilPeerEnded,
        Active,
        SortOrder,
        MenuRootNumber,
        MenuOption,
        MenuParentCode,
        MenuSortOrder,
        IsMenuEntry,
        IsDirectDialAllowed,
        CreatedAt
    )
    VALUES
    (
        @InternalNumber,
        @DisplayName,
        @Description,
        @ServiceCode,
        @ServiceType,
        @PromptFile,
        @RecordTemplate,
        @JsonConfig,
        1,
        1,
        1,
        @SortOrder,
        @MenuRootNumber,
        @MenuOption,
        @MenuParentCode,
        @MenuSortOrder,
        1,
        1,
        SYSDATETIME()
    );
END";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@InternalNumber", "*100*4#");
            cmd.Parameters.AddWithValue("@DisplayName", "GesprÃƒÂ¤ch aufnehmen");
            cmd.Parameters.AddWithValue("@Description", "Laufende GesprÃƒÂ¤chsaufnahme ein- oder ausschalten.");
            cmd.Parameters.AddWithValue("@ServiceCode", SIP_ServiceCodes.CallRecordingToggle);
            cmd.Parameters.AddWithValue("@ServiceType", TKSVC_ServiceTypes.CallRecording);
            cmd.Parameters.AddWithValue("@PromptFile", @"Prompts\call_recording.wav");
            cmd.Parameters.AddWithValue("@RecordTemplate", @"Recordings\call_{username}_{sessionid}_{timestamp}.wav");
            cmd.Parameters.AddWithValue("@JsonConfig", @"{""Scope"":""InCall"",""MenuRootNumber"":""*100"",""MenuOption"":""*4"",""Pattern"":""*100*4#"",""NeedsTarget"":false,""Mode"":""Toggle"",""StoreMode"":""FileSystem"",""AnnounceStart"":true,""AnnounceStop"":true}");
            cmd.Parameters.AddWithValue("@SortOrder", 140);
            cmd.Parameters.AddWithValue("@MenuRootNumber", "*100");
            cmd.Parameters.AddWithValue("@MenuOption", "*4");
            cmd.Parameters.AddWithValue("@MenuParentCode", SIP_ServiceCodes.MainMenu);
            cmd.Parameters.AddWithValue("@MenuSortOrder", 4);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The UpsertMailboxGreetingBlob
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="audioBlob">The audioBlob<see cref="byte[]"/></param>
        /// <param name="durationMs">The durationMs<see cref="int?"/></param>
        /// <param name="sampleRateHz">The sampleRateHz<see cref="int?"/></param>
        /// <param name="channels">The channels<see cref="int?"/></param>
        /// <param name="jsonConfig">The jsonConfig<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpsertMailboxGreetingBlob(
            int userId,
            byte[] audioBlob,
            int? durationMs = null,
            int? sampleRateHz = null,
            int? channels = null,
            string? jsonConfig = null) {

            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId), "UserID muss groesser 0 sein.");
            if (audioBlob == null || audioBlob.Length == 0) throw new ArgumentException("AudioBlob darf nicht leer sein.", nameof(audioBlob));

            using SqlCommand cmd = new SqlCommand(@"
MERGE dbo.TK_MailBoxMessage WITH (HOLDLOCK) AS target
USING (SELECT @UserID AS UserID) AS src
ON target.UserID = src.UserID
WHEN MATCHED THEN
    UPDATE SET
        AudioKey = @AudioKey,
        DisplayName = @DisplayName,
        Description = @Description,
        AudioRole = N'Prompt',
        FileNameOriginal = @FileNameOriginal,
        FileExtension = N'.wav',
        ContentType = N'audio/wav',
        AudioFormat = N'WAV',
        CodecID = NULL,
        SampleRateHz = @SampleRateHz,
        Channels = @Channels,
        Bitrate = NULL,
        DurationMs = @DurationMs,
        FileSizeBytes = DATALENGTH(@AudioBlob),
        ContentHashSha256 = HASHBYTES('SHA2_256', @AudioBlob),
        AudioBlob = @AudioBlob,
        JsonConfig = @JsonConfig,
        Active = 1,
        SortOrder = 0,
        UpdatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (
        GUID,
        UserID,
        AudioKey,
        DisplayName,
        Description,
        AudioRole,
        FileNameOriginal,
        FileExtension,
        ContentType,
        AudioFormat,
        CodecID,
        SampleRateHz,
        Channels,
        Bitrate,
        DurationMs,
        FileSizeBytes,
        ContentHashSha256,
        AudioBlob,
        JsonConfig,
        Active,
        SortOrder,
        CreatedAt,
        UpdatedAt)
    VALUES (
        NEWID(),
        @UserID,
        @AudioKey,
        @DisplayName,
        @Description,
        N'Prompt',
        @FileNameOriginal,
        N'.wav',
        N'audio/wav',
        N'WAV',
        NULL,
        @SampleRateHz,
        @Channels,
        NULL,
        @DurationMs,
        DATALENGTH(@AudioBlob),
        HASHBYTES('SHA2_256', @AudioBlob),
        @AudioBlob,
        @JsonConfig,
        1,
        0,
        SYSDATETIME(),
        NULL);", TKSQL_ConnectionClass.GetConnection());

            string audioKey = $"mailbox_greeting_{userId}";
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@AudioKey", audioKey);
            cmd.Parameters.AddWithValue("@DisplayName", "Mailboxansage");
            cmd.Parameters.AddWithValue("@Description", "Aktive Mailboxansage des Benutzers.");
            cmd.Parameters.AddWithValue("@FileNameOriginal", $"{audioKey}.wav");
            cmd.Parameters.AddWithValue("@SampleRateHz", (object?)sampleRateHz ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Channels", (object?)channels ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DurationMs", (object?)durationMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AudioBlob", audioBlob);
            cmd.Parameters.AddWithValue("@JsonConfig", (object?)jsonConfig ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }


        /// <summary>
        /// The LoadMailboxGreetingBlob
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <returns>The <see cref="byte[]?"/></returns>
        public byte[]? LoadMailboxGreetingBlob(int userId) {
            if (userId <= 0) {
                return null;
            }

            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    AudioBlob
FROM dbo.TK_MailBoxMessage
WHERE UserID = @UserID
  AND Active = 1
  AND AudioBlob IS NOT NULL
ORDER BY UpdatedAt DESC, CreatedAt DESC, ID DESC;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@UserID", userId);
            DataRow? row = ExecuteSingleRow(cmd);
            if (row == null || row["AudioBlob"] == DBNull.Value) {
                return null;
            }

            return (byte[])row["AudioBlob"];
        }

        /// <summary>
        /// Stellt die TK_Properties-Spalten fuer die oeffentliche Internet-IP bereit.
        /// </summary>
        private void EnsurePublicInternetIpColumns() {
            const string query = @"
IF COL_LENGTH('dbo.TK_Properties', 'PublicInternetIPv4') IS NULL
    ALTER TABLE dbo.TK_Properties ADD PublicInternetIPv4 nvarchar(64) NULL;
IF COL_LENGTH('dbo.TK_Properties', 'PublicInternetIPv6') IS NULL
    ALTER TABLE dbo.TK_Properties ADD PublicInternetIPv6 nvarchar(128) NULL;
IF COL_LENGTH('dbo.TK_Properties', 'PublicInternetIPSource') IS NULL
    ALTER TABLE dbo.TK_Properties ADD PublicInternetIPSource nvarchar(128) NULL;
IF COL_LENGTH('dbo.TK_Properties', 'PublicInternetIPLastCheckedAt') IS NULL
    ALTER TABLE dbo.TK_Properties ADD PublicInternetIPLastCheckedAt datetime2(7) NULL;
IF COL_LENGTH('dbo.TK_Properties', 'PublicInternetIPLastChangedAt') IS NULL
    ALTER TABLE dbo.TK_Properties ADD PublicInternetIPLastChangedAt datetime2(7) NULL;
IF COL_LENGTH('dbo.TK_Properties', 'PublicInternetIPLastError') IS NULL
    ALTER TABLE dbo.TK_Properties ADD PublicInternetIPLastError nvarchar(1000) NULL;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Laedt die gespeicherte oeffentliche Internet-IP aus TK_Properties.
        /// </summary>
        public DataRow? LoadPublicInternetIpState() {
            EnsurePublicInternetIpColumns();

            const string query = @"
SELECT TOP 1
    Id,
    ServerIP,
    PublicInternetIPv4,
    PublicInternetIPv6,
    PublicInternetIPSource,
    PublicInternetIPLastCheckedAt,
    PublicInternetIPLastChangedAt,
    PublicInternetIPLastError
FROM dbo.TK_Properties
ORDER BY Id DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// Gibt die gespeicherte oeffentliche Internet-IP passend zur Adressfamilie zurueck.
        /// </summary>
        public string? GetPublicInternetIpForAddressFamily(AddressFamily addressFamily) {
            DataRow? row = LoadPublicInternetIpState();
            if (row == null) {
                return null;
            }

            string columnName = addressFamily == AddressFamily.InterNetworkV6 ? "PublicInternetIPv6" : "PublicInternetIPv4";
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return null;
            }

            string value = Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// Speichert eine erfolgreich ermittelte oeffentliche Internet-IP in TK_Properties.
        /// </summary>
        public int SavePublicInternetIpSuccess(string? ipv4, string? ipv6, string source) {
            EnsurePublicInternetIpColumns();

            const string query = @"
DECLARE @Id int = (SELECT TOP 1 Id FROM dbo.TK_Properties ORDER BY Id DESC);
IF @Id IS NULL
    RETURN;

UPDATE dbo.TK_Properties
SET
    PublicInternetIPLastChangedAt = CASE
        WHEN ISNULL(PublicInternetIPv4, N'') <> ISNULL(@IPv4, N'')
          OR ISNULL(PublicInternetIPv6, N'') <> ISNULL(@IPv6, N'')
        THEN SYSUTCDATETIME()
        ELSE PublicInternetIPLastChangedAt
    END,
    PublicInternetIPv4 = @IPv4,
    PublicInternetIPv6 = @IPv6,
    PublicInternetIPSource = @Source,
    PublicInternetIPLastCheckedAt = SYSUTCDATETIME(),
    PublicInternetIPLastError = NULL,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@IPv4", DbOptionalString(ipv4));
            cmd.Parameters.AddWithValue("@IPv6", DbOptionalString(ipv6));
            cmd.Parameters.AddWithValue("@Source", DbOptionalString(source));
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Speichert den letzten Fehler der oeffentlichen Internet-IP-Ermittlung.
        /// </summary>
        public int SavePublicInternetIpFailure(string errorText) {
            EnsurePublicInternetIpColumns();

            const string query = @"
DECLARE @Id int = (SELECT TOP 1 Id FROM dbo.TK_Properties ORDER BY Id DESC);
IF @Id IS NULL
    RETURN;

UPDATE dbo.TK_Properties
SET
    PublicInternetIPLastCheckedAt = SYSUTCDATETIME(),
    PublicInternetIPLastError = @LastError,
    UpdatedAt = SYSDATETIME()
WHERE Id = @Id;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@LastError", DbOptionalString(errorText));
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The LoadServerSettings
        /// </summary>
        /// <returns>The <see cref="TKBase_ServerSettings"/></returns>
        internal TKBase_ServerSettings LoadServerSettings() {
            const string query = @"
SELECT TOP 1
    ServerIP,
    SipProxy,
    Registrar,
    Realm,
    StunServer,
    OutboundProxy,
    SipTransport,
    SipPort,
    UseSRTP,
    RtpPortStart,
    RtpPortEnd,
    GSMAudioPort,
    SIPCodec,
    GSMCodec,
    RegisterExpire,
    UseAdsProfileMediaPath,
    UserMediaRootPath,
    UserMediaFolderName
FROM dbo.TK_Properties
ORDER BY Id DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            DataRow? row = ExecuteSingleRow(cmd);
            if (row == null) {
                throw new InvalidOperationException("Keine Servereinstellungen in TK_Properties gefunden.");
            }

            return TKBase_ServerSettings.FromDataRow(row);
        }

        /// <summary>
        /// The GetSipUserByUsername
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetSipUserByUsername(string username) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_SIP_User
WHERE Username = @Username
  AND Active = 1;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@Username", username ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetActiveSipUsers
        /// </summary>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetActiveSipUsers() {
            const string query = @"
SELECT
    ID,
    Username,
    Telefonnummer,
    DisplayName,
    UserType,
    AdsProfilePath,
    Active,
    SortOrder
FROM dbo.TK_SIP_User
WHERE Active = 1
ORDER BY SortOrder ASC, Telefonnummer ASC, Username ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The GetSipUserByNumber
        /// </summary>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetSipUserByNumber(string number) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_SIP_User
WHERE Telefonnummer = @Telefonnummer
  AND Active = 1;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@Telefonnummer", number ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetSipUserById
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetSipUserById(int userId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_SIP_User
WHERE ID = @ID
  AND Active = 1;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ID", userId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetActiveRegistrationByUserId
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetActiveRegistrationByUserId(int userId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_RuntimeRegistration
WHERE RegistrationType = 'Internal'
  AND UserID = @UserID
  AND IsRegistered = 1
  AND IsReachable = 1
  AND (ExpiresAt IS NULL OR ExpiresAt > SYSDATETIME())
ORDER BY LastSeen DESC, ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@UserID", userId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetActiveRegistrationsByUserId
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetActiveRegistrationsByUserId(int userId) {
            const string query = @"
SELECT *
FROM dbo.TK_RuntimeRegistration
WHERE RegistrationType = 'Internal'
  AND UserID = @UserID
  AND IsRegistered = 1
  AND IsReachable = 1
  AND (ExpiresAt IS NULL OR ExpiresAt > SYSDATETIME())
ORDER BY LastSeen DESC, ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@UserID", userId);
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The GetActiveRegistrationsByNumber
        /// </summary>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetActiveRegistrationsByNumber(string number) {
            const string query = @"
SELECT rr.*
FROM dbo.TK_RuntimeRegistration rr
INNER JOIN dbo.TK_SIP_User u ON u.ID = rr.UserID
WHERE rr.RegistrationType = 'Internal'
  AND u.Telefonnummer = @Telefonnummer
  AND u.Active = 1
  AND rr.IsRegistered = 1
  AND rr.IsReachable = 1
  AND (rr.ExpiresAt IS NULL OR rr.ExpiresAt > SYSDATETIME())
ORDER BY rr.LastSeen DESC, rr.ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@Telefonnummer", number ?? string.Empty);
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The GetDefaultActiveProviderAccount
        /// </summary>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetDefaultActiveProviderAccount() {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ProviderAccounts
WHERE ISNULL(Active, 0) = 1
ORDER BY ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// Bereinigt volatile Runtime-Zustaende.
        /// Beim Start duerfen Live-Calls und Registrierungen nicht geloescht werden, weil die GUI ihren Live-Status aus den Runtime-Tabellen liest.
        /// Beim echten Runtime-Ende duerfen Live-Calls entfernt und Registrierungen/Online-Zustaende offline gesetzt werden.
        /// </summary>
        /// <param name="clearCalls">True entfernt Live-Calls aus dbo.TK_RuntimeCall.</param>
        /// <param name="clearRegistrations">True setzt Registrierungen/Online-Zustaende offline.</param>
        public void ResetVolatileRuntimeState(bool clearCalls, bool clearRegistrations) {
            const string clearCallsQuery = @"
DELETE FROM dbo.TK_RuntimeCall;
";

            const string clearRegistrationsQuery = @"
UPDATE dbo.TK_InternalRegistrationState
SET
    IsRegistered = 0,
    IsReachable = 0,
    LastError = N'Serverende Reset',
    UpdatedAt = SYSDATETIME();

UPDATE dbo.TK_RuntimeRegistration
SET
    IsRegistered = 0,
    IsReachable = 0,
    LastError = N'Serverende Reset',
    UpdatedAt = SYSDATETIME();

UPDATE dbo.TK_ProviderRegistrationState
SET
    IsRegistered = 0,
    LastError = N'Serverende Reset',
    UpdatedAt = SYSUTCDATETIME();

UPDATE dbo.TK_ExternalNumbers
SET
    IsOnline = 0,
    UpdatedAt = SYSDATETIME()
WHERE SourceType IN (N'ProviderAccount', N'GSM');
";

            string sql = string.Empty;

            if (clearCalls) {
                sql += clearCallsQuery;
            }

            if (clearRegistrations) {
                sql += clearRegistrationsQuery;
            }

            if (string.IsNullOrWhiteSpace(sql)) {
                return;
            }

            using SqlCommand cmd = new SqlCommand(sql, TKSQL_ConnectionClass.GetConnection());
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Prueft, ob aktuell Live-Calls in der Runtime-Tabelle stehen.
        /// Diese Abfrage wird vor Service-Handoffs verwendet, damit laufende Calls nicht durch Stop/Start-Uebergaben beendet werden.
        /// </summary>
        /// <returns>True, wenn aktive Runtime-Calls vorhanden sind.</returns>
        public bool HasActiveRuntimeCalls() {
            const string query = @"
IF OBJECT_ID('dbo.TK_RuntimeCall', 'U') IS NULL
BEGIN
    SELECT CAST(0 AS bit) AS HasActiveCalls;
END
ELSE
BEGIN
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.TK_RuntimeCall
        WHERE Status IN (N'Trying', N'Ringing', N'Answered', N'Holding', N'Transferring')
    ) THEN 1 ELSE 0 END AS bit) AS HasActiveCalls;
END;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            object? result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value && Convert.ToBoolean(result);
        }


        /// <summary>
        /// LÃƒÂ¤dt den Provider-Registrierungsstatus aus der Datenbank.
        /// Wichtig fÃƒÂ¼r den Servicebetrieb: Die GUI lÃƒÂ¤uft in einem anderen Prozess und darf nicht den lokalen In-Memory-Status aus TKBase_RegistrationManager verwenden.
        /// </summary>
        /// <returns>Dictionary ProviderAccountID -> IsRegistered</returns>
        public Dictionary<int, bool> GetProviderRegistrationStatesFromDatabase() {
            const string query = @"
SELECT
    p.ID AS ProviderAccountID,
    CAST(CASE
        WHEN ISNULL(prs.IsRegistered, 0) = 1 THEN 1
        WHEN EXISTS (
            SELECT 1
            FROM dbo.TK_RuntimeRegistration rr
            WHERE rr.RegistrationType = 'External'
              AND rr.SourceType = 'ProviderAccount'
              AND rr.SourceID = p.ID
              AND rr.IsRegistered = 1
              AND rr.IsReachable = 1
              AND (rr.ExpiresAt IS NULL OR rr.ExpiresAt > SYSDATETIME())
        ) THEN 1
        ELSE 0
    END AS bit) AS IsRegistered
FROM dbo.TK_ProviderAccounts p
LEFT JOIN dbo.TK_ProviderRegistrationState prs
    ON prs.ProviderAccountID = p.ID
WHERE ISNULL(p.Active, 0) = 1
ORDER BY p.ID ASC;";

            Dictionary<int, bool> result = new();
            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read()) {
                int providerAccountId = reader["ProviderAccountID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ProviderAccountID"]);
                bool isRegistered = reader["IsRegistered"] != DBNull.Value && Convert.ToBoolean(reader["IsRegistered"]);

                if (providerAccountId > 0) {
                    result[providerAccountId] = isRegistered;
                }
            }

            return result;
        }

        /// <summary>
        /// LÃƒÂ¤dt den GSM-Dashboardstatus aus der Datenbank.
        /// Wichtig fÃƒÂ¼r den Servicebetrieb: Die GUI lÃƒÂ¤uft in einem anderen Prozess und darf nicht den lokalen In-Memory-Status aus TKBase_GsmGateway verwenden.
        /// </summary>
        /// <returns>GSM-Dashboarddaten</returns>
        public DataTable GetGsmDashboardStateFromDatabase() {
            const string query = @"
SELECT
    ms.ID,
    COALESCE(NULLIF(s.Telefonnummer, ''), NULLIF(en.NumberValue, ''), '-') AS Telefonnummer,
    COALESCE(NULLIF(m.IMEI, ''), '-') AS IMEI,
    CAST(CASE
        WHEN ISNULL(en.IsOnline, 0) = 1 THEN 1
        WHEN ms.LastSeen IS NOT NULL AND ms.LastSeen >= DATEADD(SECOND, -90, SYSDATETIME()) THEN 1
        ELSE 0
    END AS bit) AS IsServiceStarted
FROM dbo.TK_GSM_ModemSimStatus ms
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
LEFT JOIN dbo.TK_GSM_Modems m
    ON m.ID = ms.ModemID
LEFT JOIN dbo.TK_ExternalNumbers en
    ON en.SourceType = 'GSMStatus'
   AND en.SourceID = ms.ID
ORDER BY
    CASE WHEN ms.LastSeen IS NULL THEN 1 ELSE 0 END,
    ms.LastSeen DESC,
    ms.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// The GetProviderAccountById
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetProviderAccountById(int providerAccountId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ProviderAccounts
WHERE ID = @ID
  AND ISNULL(Active, 0) = 1
ORDER BY ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ID", providerAccountId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetProviderRegistrationStateByProviderAccountId
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetProviderRegistrationStateByProviderAccountId(int providerAccountId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ProviderRegistrationState
WHERE ProviderAccountID = @ProviderAccountID
ORDER BY UpdatedAt DESC, ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetExternalOutboundRoutesForUser
        /// </summary>
        /// <param name="internalNumberId">The internalNumberId<see cref="int"/></param>
        /// <param name="targetType">The targetType<see cref="string?"/></param>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetExternalOutboundRoutesForUser(int internalNumberId, string? targetType) {
            const string query = @"
SELECT
    r.*,
    en.NumberValue,
    en.DisplayName,
    en.IsOnline,
    en.IsSelectable,
    en.Active AS ExternalNumberActive,
    en.SourceType,
    en.SourceID,
    pa.ProviderName,
    pa.SIPUser,
    pa.SIPNr,
    pa.SipAuthUser,
    pa.SIPPasswort,
    pa.SIPProtokoll,
    pa.SIPPort,
    pa.SIPOutboundProxy,
    pa.SIPProxy,
    pa.SIPRegistrar,
    pa.SIPServer,
    pa.Active AS ProviderIsActive
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN dbo.TK_ExternalNumbers en ON en.ID = r.ExternalNumberID
LEFT JOIN dbo.TK_ProviderAccounts pa
    ON en.SourceType = 'ProviderAccount'
   AND pa.ID = en.SourceID
WHERE r.InternalNumberID = @InternalNumberID
  AND r.Active = 1
  AND (
        @TargetType = ''
        OR r.TargetType = @TargetType
        OR r.TargetType = 'Alle'
        OR r.TargetType = 'Any'
      )
ORDER BY r.Priority ASC, r.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@InternalNumberID", internalNumberId);
            cmd.Parameters.AddWithValue("@TargetType", (object?)(targetType ?? string.Empty) ?? string.Empty);
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The GetActiveRegistrationByNumber
        /// </summary>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetActiveRegistrationByNumber(string number) {
            const string query = @"
SELECT TOP 1 rr.*
FROM dbo.TK_RuntimeRegistration rr
INNER JOIN dbo.TK_SIP_User u ON u.ID = rr.UserID
WHERE rr.RegistrationType = 'Internal'
  AND u.Telefonnummer = @Telefonnummer
  AND u.Active = 1
  AND rr.IsRegistered = 1
  AND rr.IsReachable = 1
  AND (rr.ExpiresAt IS NULL OR rr.ExpiresAt > SYSDATETIME())
ORDER BY rr.LastSeen DESC, rr.ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@Telefonnummer", number ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetExternalNumberBySource
        /// </summary>
        /// <param name="sourceType">The sourceType<see cref="string"/></param>
        /// <param name="sourceId">The sourceId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetExternalNumberBySource(string sourceType, int sourceId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ExternalNumbers
WHERE SourceType = @SourceType
  AND SourceID = @SourceID
ORDER BY ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SourceType", sourceType ?? string.Empty);
            cmd.Parameters.AddWithValue("@SourceID", sourceId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetExternalNumberByValue
        /// </summary>
        /// <param name="numberValue">The numberValue<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetExternalNumberByValue(string numberValue) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ExternalNumbers
WHERE NumberValue = @NumberValue
ORDER BY ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@NumberValue", numberValue ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetInboundExternalRoutesByCalledNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetInboundExternalRoutesByCalledNumber(string calledNumber) {
            const string query = @"
SELECT
    r.*,
    en.NumberValue,
    en.DisplayName,
    en.IsOnline,
    en.IsSelectable,
    en.Active AS ExternalNumberActive,
    en.SourceType,
    en.SourceID,
    u.ID AS TargetUserID,
    u.Username AS TargetUsername,
    u.Telefonnummer AS TargetTelefonnummer,
    u.UserType AS TargetUserType,
    u.Active AS TargetUserActive,
    u.CanReceiveExternal,
    reg.ID AS RegistrationID,
    reg.ContactURI,
    reg.RemoteEndPoint,
    reg.Transport,
    reg.ExpiresAt,
    reg.LastSeen,
    reg.IsRegistered AS RegistrationActive
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN dbo.TK_ExternalNumbers en ON en.ID = r.ExternalNumberID
LEFT JOIN dbo.TK_SIP_User u ON u.ID = r.InternalNumberID
OUTER APPLY (
    SELECT TOP 1
        reg.ID,
        reg.ContactURI,
        reg.RemoteEndPoint,
        reg.Transport,
        reg.ExpiresAt,
        reg.LastSeen,
        reg.IsRegistered
    FROM dbo.TK_RuntimeRegistration reg
    WHERE reg.RegistrationType = 'Internal'
      AND reg.UserID = u.ID
      AND reg.IsRegistered = 1
      AND reg.IsReachable = 1
      AND (reg.ExpiresAt IS NULL OR reg.ExpiresAt > SYSDATETIME())
    ORDER BY reg.LastSeen DESC, reg.ID DESC
) reg
WHERE en.NumberValue = @NumberValue
  AND en.Active = 1
  AND r.Active = 1
ORDER BY r.Priority ASC, r.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@NumberValue", calledNumber ?? string.Empty);
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The GetInboundExternalRouteByCalledNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetInboundExternalRouteByCalledNumber(string calledNumber) {
            List<DataRow> routes = GetInboundExternalRoutesByCalledNumber(calledNumber);
            return routes.FirstOrDefault();
        }

        /// <summary>
        /// The GetProviderAccountByNumber
        /// </summary>
        /// <param name="numberValue">The numberValue<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetProviderAccountByNumber(string numberValue) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ProviderAccounts
WHERE SIPUser = @NumberValue
   OR SIPNr = @NumberValue
ORDER BY ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@NumberValue", numberValue ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The UpsertExternalNumberFromProvider
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <param name="numberValue">The numberValue<see cref="string"/></param>
        /// <param name="displayName">The displayName<see cref="string?"/></param>
        /// <param name="isOnline">The isOnline<see cref="bool"/></param>
        /// <param name="isSelectable">The isSelectable<see cref="bool"/></param>
        /// <param name="active">The active<see cref="bool"/></param>
        /// <param name="numberType">The numberType<see cref="string"/></param>
        /// <param name="sourceType">The sourceType<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpsertExternalNumberFromProvider(
            int providerAccountId,
            string numberValue,
            string? displayName,
            bool isOnline,
            bool isSelectable,
            bool active = true,
            string numberType = "Provider",
            string sourceType = "ProviderAccount") {

            DataRow? existing = GetExternalNumberBySource(sourceType, providerAccountId);
            if (existing == null && !string.IsNullOrWhiteSpace(numberValue)) {
                existing = GetExternalNumberByValue(numberValue);
            }

            if (existing != null) {
                int existingId = Convert.ToInt32(existing["ID"]);

                using SqlCommand updateCmd = new SqlCommand(@"
UPDATE dbo.TK_ExternalNumbers
SET
    NumberType = @NumberType,
    SourceID = @SourceID,
    NumberValue = @NumberValue,
    DisplayName = @DisplayName,
    IsOnline = @IsOnline,
    IsSelectable = @IsSelectable,
    Active = @Active,
    UpdatedAt = SYSDATETIME(),
    SourceType = @SourceType
WHERE ID = @ID;", TKSQL_ConnectionClass.GetConnection());

                updateCmd.Parameters.AddWithValue("@ID", existingId);
                updateCmd.Parameters.AddWithValue("@NumberType", numberType ?? "Provider");
                updateCmd.Parameters.AddWithValue("@SourceID", providerAccountId);
                updateCmd.Parameters.AddWithValue("@NumberValue", numberValue ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@DisplayName", (object?)displayName ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@IsOnline", isOnline);
                updateCmd.Parameters.AddWithValue("@IsSelectable", isSelectable);
                updateCmd.Parameters.AddWithValue("@Active", active);
                updateCmd.Parameters.AddWithValue("@SourceType", sourceType ?? "ProviderAccount");
                updateCmd.ExecuteNonQuery();

                _logger?.Info($"ExternalNumbers aktualisiert | ID={existingId} | SourceType={sourceType} | SourceID={providerAccountId} | Number={numberValue} | Online={isOnline} | Selectable={isSelectable}");
                return existingId;
            }

            using SqlCommand insertCmd = new SqlCommand(@"
INSERT INTO dbo.TK_ExternalNumbers
(
    NumberType,
    SourceID,
    NumberValue,
    DisplayName,
    IsOnline,
    IsSelectable,
    Active,
    CreatedAt,
    UpdatedAt,
    SourceType
)
VALUES
(
    @NumberType,
    @SourceID,
    @NumberValue,
    @DisplayName,
    @IsOnline,
    @IsSelectable,
    @Active,
    SYSDATETIME(),
    SYSDATETIME(),
    @SourceType
);

SELECT CAST(SCOPE_IDENTITY() AS int);", TKSQL_ConnectionClass.GetConnection());

            insertCmd.Parameters.AddWithValue("@NumberType", numberType ?? "Provider");
            insertCmd.Parameters.AddWithValue("@SourceID", providerAccountId);
            insertCmd.Parameters.AddWithValue("@NumberValue", numberValue ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@DisplayName", (object?)displayName ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@IsOnline", isOnline);
            insertCmd.Parameters.AddWithValue("@IsSelectable", isSelectable);
            insertCmd.Parameters.AddWithValue("@Active", active);
            insertCmd.Parameters.AddWithValue("@SourceType", sourceType ?? "ProviderAccount");

            int id = Convert.ToInt32(insertCmd.ExecuteScalar());
            _logger?.Info($"ExternalNumbers angelegt | ID={id} | SourceType={sourceType} | SourceID={providerAccountId} | Number={numberValue} | Online={isOnline} | Selectable={isSelectable}");
            return id;
        }

        /// <summary>
        /// The SetExternalNumberOnlineState
        /// </summary>
        /// <param name="sourceType">The sourceType<see cref="string"/></param>
        /// <param name="sourceId">The sourceId<see cref="int"/></param>
        /// <param name="isOnline">The isOnline<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int SetExternalNumberOnlineState(string sourceType, int sourceId, bool isOnline) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_ExternalNumbers
SET
    IsOnline = @IsOnline,
    UpdatedAt = SYSDATETIME()
WHERE SourceType = @SourceType
  AND SourceID = @SourceID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@IsOnline", isOnline);
            cmd.Parameters.AddWithValue("@SourceType", sourceType ?? string.Empty);
            cmd.Parameters.AddWithValue("@SourceID", sourceId);

            int rows = cmd.ExecuteNonQuery();
            if (rows > 0) {
                _logger?.Info($"ExternalNumbers Online-Status gesetzt | SourceType={sourceType} | SourceID={sourceId} | Online={isOnline} | Rows={rows}");
            }
            return rows;
        }

        /// <summary>
        /// The UpsertExternalNumberFromGsmStatus
        /// </summary>
        /// <param name="gsmStatusId">The gsmStatusId<see cref="int"/></param>
        /// <param name="isOnline">The isOnline<see cref="bool"/></param>
        /// <param name="isSelectable">The isSelectable<see cref="bool"/></param>
        /// <param name="active">The active<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpsertExternalNumberFromGsmStatus(int gsmStatusId, bool isOnline, bool isSelectable, bool active = true) {
            const string query = @"
SELECT TOP 1
    ms.ID AS StatusID,
    s.Telefonnummer,
    m.ModemName,
    m.IMEI
FROM dbo.TK_GSM_ModemSimStatus ms
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
LEFT JOIN dbo.TK_GSM_Modems m
    ON m.ID = ms.ModemID
WHERE ms.ID = @StatusID;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@StatusID", gsmStatusId);
            DataRow? row = ExecuteSingleRow(cmd);
            if (row == null) {
                _logger?.Warn($"ExternalNumbers GSM Sync ÃƒÂ¼bersprungen | StatusID={gsmStatusId} | Grund=Status nicht gefunden");
                return 0;
            }

            string numberValue = row["Telefonnummer"] == DBNull.Value ? string.Empty : row["Telefonnummer"].ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(numberValue)) {
                _logger?.Warn($"ExternalNumbers GSM Sync ÃƒÂ¼bersprungen | StatusID={gsmStatusId} | Grund=Keine Telefonnummer in TK_GSM_Sim");
                return 0;
            }

            string displayName = row["ModemName"] == DBNull.Value ? numberValue : row["ModemName"].ToString()?.Trim() ?? numberValue;
            return UpsertExternalNumberFromProvider(
                gsmStatusId,
                numberValue,
                displayName,
                isOnline,
                isSelectable,
                active,
                numberType: "GSM",
                sourceType: "GSMStatus");
        }

        /// <summary>
        /// Deaktiviert alle VoIP-/externen Nummern und Routen eines ProviderAccounts.
        /// PrioritÃƒÂ¤ten bleiben als Wiederherstellungsanker erhalten; doppelte PrioritÃƒÂ¤ten pro internem Ziel werden auf den nÃƒÂ¤chsten freien grÃƒÂ¶ÃƒÅ¸eren Wert verschoben.
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DeactivateProviderExternalNumbersAndRouting(int providerAccountId) {
            SqlConnection con = TKSQL_ConnectionClass.GetConnection();
            using SqlTransaction tx = con.BeginTransaction();

            try {
                bool hasRestorePriority = SqlColumnExists(con, tx, "TK_ExternalNumberRouting", "RestorePriority");
                List<int> affectedInternalIds = new List<int>();
                int rows = 0;

                List<ExternalRoutingRouteRow> providerRoutes = new List<ExternalRoutingRouteRow>();
                using (SqlCommand selectRoutes = new SqlCommand($@"
SELECT
    r.ID,
    r.InternalNumberID,
    r.Priority,
    r.Active{(hasRestorePriority ? ", r.RestorePriority" : string.Empty)}
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN dbo.TK_ExternalNumbers en ON en.ID = r.ExternalNumberID
WHERE en.SourceType = N'ProviderAccount'
  AND en.SourceID = @ProviderAccountID
ORDER BY r.InternalNumberID ASC, r.Priority ASC, r.ID ASC;", con, tx)) {
                    selectRoutes.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
                    using SqlDataReader reader = selectRoutes.ExecuteReader();
                    while (reader.Read()) {
                        int priority = Convert.ToInt32(reader["Priority"]);
                        int restorePriority = hasRestorePriority && reader["RestorePriority"] != DBNull.Value
                            ? Convert.ToInt32(reader["RestorePriority"])
                            : priority;
                        providerRoutes.Add(new ExternalRoutingRouteRow(
                            Convert.ToInt32(reader["ID"]),
                            Convert.ToInt32(reader["InternalNumberID"]),
                            priority,
                            Convert.ToBoolean(reader["Active"]),
                            restorePriority));
                    }
                }

                foreach (ExternalRoutingRouteRow route in providerRoutes) {
                    affectedInternalIds.Add(route.InternalNumberId);

                    if (route.Active) {
                        int inactivePriority = GetNextFreeRoutingPriority(con, tx, route.InternalNumberId, active: false, requestedPriority: route.Priority, excludeRouteId: route.RouteId);
                        UpdateExternalRoutingActivationState(con, tx, route.RouteId, active: false, inactivePriority, hasRestorePriority ? route.Priority : (int?)null);
                        rows++;
                    } else {
                        UpdateExternalRoutingActivationState(con, tx, route.RouteId, active: false, route.Priority, hasRestorePriority ? route.RestorePriority : (int?)null);
                        rows++;
                    }
                }

                using (SqlCommand updateNumbers = new SqlCommand(@"
UPDATE dbo.TK_ExternalNumbers
SET Active = 0,
    IsSelectable = 0,
    IsOnline = 0,
    UpdatedAt = SYSDATETIME()
WHERE SourceType = N'ProviderAccount'
  AND SourceID = @ProviderAccountID;", con, tx)) {
                    updateNumbers.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
                    rows += updateNumbers.ExecuteNonQuery();
                }

                NormalizeExternalRoutingPriorities(con, tx, affectedInternalIds.Distinct().ToList());
                EnsureUniqueExternalRoutingPriorities(con, tx);
                tx.Commit();

                if (rows > 0) {
                    _logger?.Info($"ProviderAccount Folgeaktion | ProviderAccountID={providerAccountId} | VoIP-Nummern/Routen deaktiviert | Rows={rows}");
                }

                return rows;
            } catch {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Aktiviert die VoIP-/externen Nummern und Routen eines ProviderAccounts wieder.
        /// Die sichtbare aktive PrioritÃƒÂ¤t wird aus RestorePriority zurÃƒÂ¼ckgefÃƒÂ¼hrt; aktive Konflikte werden sauber verschoben.
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int ActivateProviderExternalNumbersAndRouting(int providerAccountId) {
            SqlConnection con = TKSQL_ConnectionClass.GetConnection();
            using SqlTransaction tx = con.BeginTransaction();

            try {
                bool hasRestorePriority = SqlColumnExists(con, tx, "TK_ExternalNumberRouting", "RestorePriority");
                List<int> affectedInternalIds = new List<int>();
                int rows = 0;

                using (SqlCommand updateNumbers = new SqlCommand(@"
UPDATE dbo.TK_ExternalNumbers
SET Active = 1,
    IsSelectable = 1,
    UpdatedAt = SYSDATETIME()
WHERE SourceType = N'ProviderAccount'
  AND SourceID = @ProviderAccountID;", con, tx)) {
                    updateNumbers.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
                    rows += updateNumbers.ExecuteNonQuery();
                }

                List<ExternalRoutingRouteRow> providerRoutes = new List<ExternalRoutingRouteRow>();
                using (SqlCommand selectRoutes = new SqlCommand($@"
SELECT
    r.ID,
    r.InternalNumberID,
    r.Priority,
    r.Active{(hasRestorePriority ? ", r.RestorePriority" : string.Empty)}
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN dbo.TK_ExternalNumbers en ON en.ID = r.ExternalNumberID
WHERE en.SourceType = N'ProviderAccount'
  AND en.SourceID = @ProviderAccountID
ORDER BY r.InternalNumberID ASC, {(hasRestorePriority ? "ISNULL(r.RestorePriority, r.Priority)" : "r.Priority")} ASC, r.Priority ASC, r.ID ASC;", con, tx)) {
                    selectRoutes.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
                    using SqlDataReader reader = selectRoutes.ExecuteReader();
                    while (reader.Read()) {
                        int priority = Convert.ToInt32(reader["Priority"]);
                        int restorePriority = hasRestorePriority && reader["RestorePriority"] != DBNull.Value
                            ? Convert.ToInt32(reader["RestorePriority"])
                            : priority;
                        providerRoutes.Add(new ExternalRoutingRouteRow(
                            Convert.ToInt32(reader["ID"]),
                            Convert.ToInt32(reader["InternalNumberID"]),
                            priority,
                            Convert.ToBoolean(reader["Active"]),
                            restorePriority));
                    }
                }

                foreach (ExternalRoutingRouteRow route in providerRoutes) {
                    affectedInternalIds.Add(route.InternalNumberId);
                    int requestedPriority = hasRestorePriority && route.RestorePriority > 0
                        ? route.RestorePriority
                        : route.Priority;
                    int targetPriority = requestedPriority <= 0 ? 1 : requestedPriority;

                    if (!route.Active) {
                        ShiftActiveExternalRoutingPrioritiesUp(con, tx, route.InternalNumberId, targetPriority, int.MaxValue, route.RouteId, hasRestorePriority);
                    }

                    UpdateExternalRoutingActivationState(con, tx, route.RouteId, active: true, targetPriority, hasRestorePriority ? targetPriority : (int?)null);
                    rows++;
                }

                NormalizeExternalRoutingPriorities(con, tx, affectedInternalIds.Distinct().ToList());
                EnsureUniqueExternalRoutingPriorities(con, tx);
                tx.Commit();

                if (rows > 0) {
                    _logger?.Info($"ProviderAccount Folgeaktion | ProviderAccountID={providerAccountId} | VoIP-Nummern/Routen aktiviert | Rows={rows}");
                }

                return rows;
            } catch {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// The DeleteExternalNumbersAndRoutingBySource
        /// </summary>
        /// <param name="sourceType">The sourceType<see cref="string"/></param>
        /// <param name="sourceId">The sourceId<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DeleteExternalNumbersAndRoutingBySource(string sourceType, int sourceId) {
            SqlConnection con = TKSQL_ConnectionClass.GetConnection();
            using SqlTransaction tx = con.BeginTransaction();

            try {
                using SqlCommand cmd = new SqlCommand(@"
SET NOCOUNT ON;
DECLARE @Rows int = 0;
DECLARE @ExternalNumbers TABLE (ID int NOT NULL PRIMARY KEY);

INSERT INTO @ExternalNumbers (ID)
SELECT ID
FROM dbo.TK_ExternalNumbers
WHERE SourceType = @SourceType
  AND SourceID = @SourceID;

DELETE r
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN @ExternalNumbers x ON x.ID = r.ExternalNumberID;
SET @Rows += @@ROWCOUNT;

DELETE en
FROM dbo.TK_ExternalNumbers en
INNER JOIN @ExternalNumbers x ON x.ID = en.ID;
SET @Rows += @@ROWCOUNT;

SELECT @Rows;", con, tx);

                cmd.Parameters.AddWithValue("@SourceType", sourceType ?? string.Empty);
                cmd.Parameters.AddWithValue("@SourceID", sourceId);

                int rows = Convert.ToInt32(cmd.ExecuteScalar());
                tx.Commit();

                if (rows > 0) {
                    _logger?.Info($"ExternalNumbers Cleanup | SourceType={sourceType} | SourceID={sourceId} | Rows={rows}");
                }

                return rows;
            } catch {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// The DeleteExternalNumbersAndRoutingForMissingGsmCards
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int DeleteExternalNumbersAndRoutingForMissingGsmCards() {
            SqlConnection con = TKSQL_ConnectionClass.GetConnection();
            using SqlTransaction tx = con.BeginTransaction();

            try {
                using SqlCommand cmd = new SqlCommand(@"
SET NOCOUNT ON;
DECLARE @Rows int = 0;
DECLARE @ExternalNumbers TABLE (ID int NOT NULL PRIMARY KEY);

INSERT INTO @ExternalNumbers (ID)
SELECT en.ID
FROM dbo.TK_ExternalNumbers en
LEFT JOIN dbo.TK_GSM_ModemSimStatus ms
    ON en.SourceType = 'GSMStatus'
   AND en.SourceID = ms.ID
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
WHERE en.SourceType = 'GSMStatus'
  AND (
        ms.ID IS NULL
        OR ISNULL(ms.IsPresent, 0) = 0
        OR ISNULL(ms.IsSimInserted, 0) = 0
        OR ms.SimID IS NULL
        OR s.ID IS NULL
        OR ISNULL(s.Active, 0) = 0
        OR NULLIF(LTRIM(RTRIM(ISNULL(s.Telefonnummer, N''))), N'') IS NULL
      );

DELETE r
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN @ExternalNumbers x ON x.ID = r.ExternalNumberID;
SET @Rows += @@ROWCOUNT;

DELETE en
FROM dbo.TK_ExternalNumbers en
INNER JOIN @ExternalNumbers x ON x.ID = en.ID;
SET @Rows += @@ROWCOUNT;

SELECT @Rows;", con, tx);

                int rows = Convert.ToInt32(cmd.ExecuteScalar());
                tx.Commit();

                if (rows > 0) {
                    _logger?.Info($"ExternalNumbers GSM Cleanup | Rows={rows}");
                }

                return rows;
            } catch {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// The GetGsmDeviceStatusTable
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable GetGsmDeviceStatusTable() {
            const string query = @"
SELECT
    ms.ID,
    ms.ModemID,
    ms.SimID,
    ms.IsPresent,
    ms.IsSimInserted,
    ms.IsRegistered,
    ms.SignalStrength,
    ms.ComPortAT,
    ms.ComPortAudio,
    ms.ComPortDiag,
    ms.ComPortNMEA,
    ms.LastSeen,
    ms.LastError,
    s.Telefonnummer,
    s.ProviderName,
    s.ICCID,
    s.IMSI,
    s.Active AS SimActive,
    m.ModemName,
    m.Manufacturer,
    m.Model,
    m.IMEI,
    m.PNPDeviceID,
    m.Active AS ModemActive
FROM dbo.TK_GSM_ModemSimStatus ms
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
LEFT JOIN dbo.TK_GSM_Modems m
    ON m.ID = ms.ModemID
ORDER BY
    CASE WHEN ms.LastSeen IS NULL THEN 1 ELSE 0 END,
    ms.LastSeen DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// The GetGsmModemSimStatusGuidCheckTable
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable GetGsmModemSimStatusGuidCheckTable() {
            const string query = @"
SELECT
    ms.ID,
    ms.ModemID,
    ms.SimID,
    ms.GUID AS CurrentGuid,
    m.GUID AS ModemGuid,
    s.GUID AS SimGuid,
    m.ModemName,
    s.Telefonnummer,
    [dbo].[TK_GUID](
        CONCAT(
            CONVERT(nvarchar(36), m.GUID),
            N'|',
            ISNULL(CONVERT(nvarchar(36), s.GUID), N'')
        )
    ) AS ExpectedGuid
FROM [dbo].[TK_GSM_ModemSimStatus] ms
INNER JOIN [dbo].[TK_GSM_Modems] m
    ON m.ID = ms.ModemID
LEFT JOIN [dbo].[TK_GSM_Sim] s
    ON s.ID = ms.SimID
ORDER BY ms.ID;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// The ResetGsmTablesForRefresh
        /// </summary>
        public void ResetGsmTablesForRefresh() {
            using SqlConnection con = TKSQL_ConnectionClass.CreateOpenConnection();

            using SqlCommand cmd = new SqlCommand(@"
DELETE FROM [dbo].[TK_GSM_ModemSimStatus];
DBCC CHECKIDENT ('TK_GSM_ModemSimStatus', RESEED, 0);
DELETE FROM [dbo].[TK_GSM_Sim];
DBCC CHECKIDENT ('TK_GSM_Sim', RESEED, 0);
DELETE FROM [dbo].[TK_GSM_Modems];
DBCC CHECKIDENT ('TK_GSM_Modems', RESEED, 0);", con);

            cmd.ExecuteNonQuery();
            _logger?.Info("GSM-Tabellen wurden geleert und Identity-Werte zurÃƒÂ¼ckgesetzt.");
        }

        /// <summary>
        /// The GetExternalNumberRouting
        /// </summary>
        /// <param name="externalNumberId">The externalNumberId<see cref="int"/></param>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <param name="targetId">The targetId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetExternalNumberRouting(int externalNumberId, string targetType, int targetId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_ExternalNumberRouting
WHERE ExternalNumberID = @ExternalNumberID
  AND TargetType = @TargetType
  AND TargetID = @TargetID
ORDER BY Priority ASC, ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ExternalNumberID", externalNumberId);
            cmd.Parameters.AddWithValue("@TargetType", targetType ?? string.Empty);
            cmd.Parameters.AddWithValue("@TargetID", targetId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The UpsertExternalNumberRouting
        /// </summary>
        /// <param name="externalNumberId">The externalNumberId<see cref="int"/></param>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <param name="targetId">The targetId<see cref="int"/></param>
        /// <param name="priority">The priority<see cref="int"/></param>
        /// <param name="active">The active<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpsertExternalNumberRouting(
            int externalNumberId,
            string targetType,
            int targetId,
            int priority = 0,
            bool active = true) {

            DataRow? existing = GetExternalNumberRouting(externalNumberId, targetType, targetId);
            if (existing != null) {
                int existingId = Convert.ToInt32(existing["ID"]);

                using SqlCommand updateCmd = new SqlCommand(@"
UPDATE dbo.TK_ExternalNumberRouting
SET
    Priority = @Priority,
    Active = @Active,
    UpdatedAt = SYSDATETIME()
WHERE ID = @ID;", TKSQL_ConnectionClass.GetConnection());

                updateCmd.Parameters.AddWithValue("@ID", existingId);
                updateCmd.Parameters.AddWithValue("@Priority", priority);
                updateCmd.Parameters.AddWithValue("@Active", active);
                updateCmd.ExecuteNonQuery();

                _logger?.Info($"ExternalNumberRouting aktualisiert | ID={existingId} | ExternalNumberID={externalNumberId} | TargetType={targetType} | TargetID={targetId} | Priority={priority}");
                return existingId;
            }

            using SqlCommand insertCmd = new SqlCommand(@"
INSERT INTO dbo.TK_ExternalNumberRouting
(
    ExternalNumberID,
    TargetType,
    TargetID,
    Priority,
    Active,
    CreatedAt,
    UpdatedAt
)
VALUES
(
    @ExternalNumberID,
    @TargetType,
    @TargetID,
    @Priority,
    @Active,
    SYSDATETIME(),
    SYSDATETIME()
);

SELECT CAST(SCOPE_IDENTITY() AS int);", TKSQL_ConnectionClass.GetConnection());

            insertCmd.Parameters.AddWithValue("@ExternalNumberID", externalNumberId);
            insertCmd.Parameters.AddWithValue("@TargetType", targetType ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@TargetID", targetId);
            insertCmd.Parameters.AddWithValue("@Priority", priority);
            insertCmd.Parameters.AddWithValue("@Active", active);

            int id = Convert.ToInt32(insertCmd.ExecuteScalar());
            _logger?.Info($"ExternalNumberRouting angelegt | ID={id} | ExternalNumberID={externalNumberId} | TargetType={targetType} | TargetID={targetId} | Priority={priority}");
            return id;
        }

        /// <summary>
        /// The CreateCallLog
        /// </summary>
        /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
        /// <param name="fromUserId">The fromUserId<see cref="int?"/></param>
        /// <param name="toUserId">The toUserId<see cref="int?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="direction">The direction<see cref="string"/></param>
        /// <param name="status">The status<see cref="string"/></param>
        /// <param name="startTime">The startTime<see cref="DateTime?"/></param>
        /// <param name="disconnectReason">The disconnectReason<see cref="string?"/></param>
        /// <param name="bridgeId">The bridgeId<see cref="Guid?"/></param>
        /// <param name="legACallId">The legACallId<see cref="string?"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <param name="inviteInRaw">The inviteInRaw<see cref="string?"/></param>
        /// <param name="inviteOutRaw">The inviteOutRaw<see cref="string?"/></param>
        /// <param name="reinviteCount">The reinviteCount<see cref="int"/></param>
        /// <param name="byeCount">The byeCount<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int CreateCallLog(
            string sipCallId,
            int? fromUserId,
            int? toUserId,
            string fromNumber,
            string toNumber,
            string direction,
            string status,
            DateTime? startTime = null,
            string? disconnectReason = null,
            Guid? bridgeId = null,
            string? legACallId = null,
            string? legBCallId = null,
            string? inviteInRaw = null,
            string? inviteOutRaw = null,
            int reinviteCount = 0,
            int byeCount = 0) {

            using SqlCommand cmd = new SqlCommand(@"
INSERT INTO dbo.TK_SIP_CallLog
(
    SipCallID,
    FromUserID,
    ToUserID,
    FromNumber,
    ToNumber,
    Direction,
    Status,
    StartTime,
    DisconnectReason,
    CreatedAt,
    UpdatedAt,
    BridgeID,
    LegACallID,
    LegBCallID,
    InviteInRaw,
    InviteOutRaw,
    ByeInRaw,
    ByeOutRaw,
    ReinviteCount,
    ByeCount
)
VALUES
(
    @SipCallID,
    @FromUserID,
    @ToUserID,
    @FromNumber,
    @ToNumber,
    @Direction,
    @Status,
    @StartTime,
    @DisconnectReason,
    @CreatedAt,
    @UpdatedAt,
    @BridgeID,
    @LegACallID,
    @LegBCallID,
    @InviteInRaw,
    @InviteOutRaw,
    @ByeInRaw,
    @ByeOutRaw,
    @ReinviteCount,
    @ByeCount
);

SELECT CAST(SCOPE_IDENTITY() AS int);", TKSQL_ConnectionClass.GetConnection());

            DateTime now = DateTime.Now;
            cmd.Parameters.AddWithValue("@SipCallID", sipCallId ?? string.Empty);
            cmd.Parameters.AddWithValue("@FromUserID", (object?)fromUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ToUserID", (object?)toUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FromNumber", fromNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@ToNumber", toNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@Direction", direction ?? "Intern");
            cmd.Parameters.AddWithValue("@Status", status ?? "Init");
            cmd.Parameters.AddWithValue("@StartTime", startTime ?? now);
            cmd.Parameters.AddWithValue("@DisconnectReason", (object?)disconnectReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", now);
            cmd.Parameters.AddWithValue("@UpdatedAt", now);
            cmd.Parameters.AddWithValue("@BridgeID", (object?)(bridgeId ?? Guid.NewGuid()) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegACallID", (object?)(legACallId ?? sipCallId) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegBCallID", (object?)legBCallId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InviteInRaw", (object?)inviteInRaw ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InviteOutRaw", (object?)inviteOutRaw ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ByeInRaw", DBNull.Value);
            cmd.Parameters.AddWithValue("@ByeOutRaw", DBNull.Value);
            cmd.Parameters.AddWithValue("@ReinviteCount", reinviteCount);
            cmd.Parameters.AddWithValue("@ByeCount", byeCount);

            int id = Convert.ToInt32(cmd.ExecuteScalar());
            _logger?.Info($"SIP CallLog angelegt | ID={id} | CallID={sipCallId} | Status={status}");
            return id;
        }

        /// <summary>
        /// The UpdateCallLogStatus
        /// </summary>
        /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
        /// <param name="status">The status<see cref="string"/></param>
        /// <param name="answerTime">The answerTime<see cref="DateTime?"/></param>
        /// <param name="endTime">The endTime<see cref="DateTime?"/></param>
        /// <param name="disconnectReason">The disconnectReason<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateCallLogStatus(string sipCallId, string status, DateTime? answerTime = null, DateTime? endTime = null, string? disconnectReason = null) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_SIP_CallLog
SET Status = @Status,
    AnswerTime = COALESCE(@AnswerTime, AnswerTime),
    EndTime = COALESCE(@EndTime, EndTime),
    DisconnectReason = COALESCE(@DisconnectReason, DisconnectReason),
    UpdatedAt = SYSDATETIME()
WHERE SipCallID = @SipCallID OR LegACallID = @SipCallID OR LegBCallID = @SipCallID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@Status", status ?? "Unbekannt");
            cmd.Parameters.AddWithValue("@AnswerTime", (object?)answerTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EndTime", (object?)endTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DisconnectReason", (object?)disconnectReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SipCallID", sipCallId ?? string.Empty);

            int rows = cmd.ExecuteNonQuery();
            _logger?.Info($"SIP CallLog aktualisiert | CallID={sipCallId} | Status={status} | Rows={rows}");
            return rows;
        }

        /// <summary>
        /// The UpdateCallLogInviteOutbound
        /// </summary>
        /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <param name="inviteOutRaw">The inviteOutRaw<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateCallLogInviteOutbound(string sipCallId, string? legBCallId, string? inviteOutRaw) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_SIP_CallLog
SET LegBCallID = COALESCE(@LegBCallID, LegBCallID),
    InviteOutRaw = COALESCE(@InviteOutRaw, InviteOutRaw),
    UpdatedAt = SYSDATETIME()
WHERE SipCallID = @SipCallID OR LegACallID = @SipCallID OR LegBCallID = @SipCallID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@SipCallID", sipCallId ?? string.Empty);
            cmd.Parameters.AddWithValue("@LegBCallID", (object?)legBCallId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InviteOutRaw", (object?)inviteOutRaw ?? DBNull.Value);

            int rows = cmd.ExecuteNonQuery();
            _logger?.Info($"SIP CallLog InviteOut gesetzt | CallID={sipCallId} | LegB={legBCallId} | Rows={rows}");
            return rows;
        }

        /// <summary>
        /// The IncrementReinviteCount
        /// </summary>
        /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int IncrementReinviteCount(string sipCallId) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_SIP_CallLog
SET ReinviteCount = ReinviteCount + 1,
    UpdatedAt = SYSDATETIME()
WHERE SipCallID = @SipCallID OR LegACallID = @SipCallID OR LegBCallID = @SipCallID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@SipCallID", sipCallId ?? string.Empty);
            int rows = cmd.ExecuteNonQuery();
            _logger?.Info($"SIP CallLog ReinviteCount erhÃƒÂ¶ht | CallID={sipCallId} | Rows={rows}");
            return rows;
        }

        /// <summary>
        /// The RegisterByeIn
        /// </summary>
        /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
        /// <param name="byeInRaw">The byeInRaw<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int RegisterByeIn(string sipCallId, string? byeInRaw) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_SIP_CallLog
SET ByeInRaw = COALESCE(@ByeInRaw, ByeInRaw),
    ByeCount = ByeCount + 1,
    UpdatedAt = SYSDATETIME()
WHERE SipCallID = @SipCallID OR LegACallID = @SipCallID OR LegBCallID = @SipCallID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@SipCallID", sipCallId ?? string.Empty);
            cmd.Parameters.AddWithValue("@ByeInRaw", (object?)byeInRaw ?? DBNull.Value);
            int rows = cmd.ExecuteNonQuery();
            _logger?.Info($"SIP CallLog ByeIn gesetzt | CallID={sipCallId} | Rows={rows}");
            return rows;
        }

        /// <summary>
        /// The RegisterByeOut
        /// </summary>
        /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
        /// <param name="byeOutRaw">The byeOutRaw<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int RegisterByeOut(string sipCallId, string? byeOutRaw) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_SIP_CallLog
SET ByeOutRaw = COALESCE(@ByeOutRaw, ByeOutRaw),
    ByeCount = ByeCount + 1,
    UpdatedAt = SYSDATETIME()
WHERE SipCallID = @SipCallID OR LegACallID = @SipCallID OR LegBCallID = @SipCallID;", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@SipCallID", sipCallId ?? string.Empty);
            cmd.Parameters.AddWithValue("@ByeOutRaw", (object?)byeOutRaw ?? DBNull.Value);
            int rows = cmd.ExecuteNonQuery();
            _logger?.Info($"SIP CallLog ByeOut gesetzt | CallID={sipCallId} | Rows={rows}");
            return rows;
        }

        /// <summary>
        /// The SaveOrUpdateInternalRegistrationState
        /// </summary>
        /// <param name="registrationGuid">The registrationGuid<see cref="Guid"/></param>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresAt">The expiresAt<see cref="DateTime"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int SaveOrUpdateInternalRegistrationState(
            Guid registrationGuid,
            int userId,
            string username,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            DateTime expiresAt,
            bool isRegistered,
            string? errorText = null) {

            const string sql = @"
MERGE dbo.TK_InternalRegistrationState AS target
USING (
    SELECT
        @GUID AS GUID
) AS source
ON target.GUID = source.GUID
WHEN MATCHED THEN
    UPDATE SET
        UserID = @UserID,
        Username = @Username,
        ContactURI = @ContactURI,
        RemoteEndPoint = @RemoteEndPoint,
        Transport = @Transport,
        CallID = @CallID,
        UserAgent = @UserAgent,
        ExpiresAt = @ExpiresAt,
        LastSeen = SYSDATETIME(),
        IsRegistered = @IsRegistered,
        IsReachable = @IsRegistered,
        LastError = @LastError,
        UpdatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (GUID, UserID, Username, ContactURI, RemoteEndPoint, Transport, CallID, UserAgent, ExpiresAt, LastSeen, IsRegistered, IsReachable, LastError, CreatedAt, UpdatedAt)
    VALUES (@GUID, @UserID, @Username, @ContactURI, @RemoteEndPoint, @Transport, @CallID, @UserAgent, @ExpiresAt, SYSDATETIME(), @IsRegistered, @IsRegistered, @LastError, SYSDATETIME(), SYSDATETIME())
OUTPUT INSERTED.ID;";

            using SqlCommand cmd = new SqlCommand(sql, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@GUID", registrationGuid);
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@Username", username ?? string.Empty);
            cmd.Parameters.AddWithValue("@ContactURI", contactUri ?? string.Empty);
            cmd.Parameters.AddWithValue("@RemoteEndPoint", remoteEndPoint ?? string.Empty);
            cmd.Parameters.AddWithValue("@Transport", (object?)transport ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CallID", callId ?? string.Empty);
            cmd.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);
            cmd.Parameters.AddWithValue("@IsRegistered", isRegistered);
            cmd.Parameters.AddWithValue("@LastError", (object?)errorText ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// The SaveOrUpdateRuntimeRegistrationInternal
        /// </summary>
        /// <param name="registrationGuid">The registrationGuid<see cref="Guid"/></param>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="internalStateId">The internalStateId<see cref="int"/></param>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresAt">The expiresAt<see cref="DateTime"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int SaveOrUpdateRuntimeRegistrationInternal(
            Guid registrationGuid,
            int userId,
            int internalStateId,
            string username,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            DateTime expiresAt,
            bool isRegistered,
            string? errorText = null) {

            const string sql = @"
MERGE dbo.TK_RuntimeRegistration AS target
USING (SELECT @GUID AS GUID) AS source
ON target.GUID = source.GUID
WHEN MATCHED THEN
    UPDATE SET
        RegistrationType = 'Internal',
        SourceType = 'InternalUser',
        SourceID = @SourceID,
        UserID = @UserID,
        InternalStateID = @InternalStateID,
        ProviderAccountID = NULL,
        ProviderStateID = NULL,
        DisplayName = @DisplayName,
        AorUri = @AorUri,
        ContactURI = @ContactURI,
        RemoteEndPoint = @RemoteEndPoint,
        Transport = @Transport,
        CallID = @CallID,
        UserAgent = @UserAgent,
        ExpiresAt = @ExpiresAt,
        LastSeen = SYSDATETIME(),
        IsRegistered = @IsRegistered,
        IsReachable = @IsRegistered,
        LastError = @LastError,
        UpdatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (GUID, RegistrationType, SourceType, SourceID, UserID, InternalStateID, DisplayName, AorUri, ContactURI, RemoteEndPoint, Transport, CallID, UserAgent, ExpiresAt, LastSeen, IsRegistered, IsReachable, LastError, CreatedAt, UpdatedAt)
    VALUES (@GUID, 'Internal', 'InternalUser', @SourceID, @UserID, @InternalStateID, @DisplayName, @AorUri, @ContactURI, @RemoteEndPoint, @Transport, @CallID, @UserAgent, @ExpiresAt, SYSDATETIME(), @IsRegistered, @IsRegistered, @LastError, SYSDATETIME(), SYSDATETIME())
OUTPUT INSERTED.ID;";

            using SqlCommand cmd = new SqlCommand(sql, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@GUID", registrationGuid);
            cmd.Parameters.AddWithValue("@SourceID", userId);
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@InternalStateID", internalStateId);
            cmd.Parameters.AddWithValue("@DisplayName", username ?? string.Empty);
            cmd.Parameters.AddWithValue("@AorUri", username ?? string.Empty);
            cmd.Parameters.AddWithValue("@ContactURI", contactUri ?? string.Empty);
            cmd.Parameters.AddWithValue("@RemoteEndPoint", remoteEndPoint ?? string.Empty);
            cmd.Parameters.AddWithValue("@Transport", (object?)transport ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CallID", callId ?? string.Empty);
            cmd.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);
            cmd.Parameters.AddWithValue("@IsRegistered", isRegistered);
            cmd.Parameters.AddWithValue("@LastError", (object?)errorText ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// The SaveOrUpdateRuntimeRegistrationExternal
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <param name="providerName">The providerName<see cref="string"/></param>
        /// <param name="aorUri">The aorUri<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string?"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string?"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string?"/></param>
        /// <param name="serviceRoute">The serviceRoute<see cref="string?"/></param>
        /// <param name="associatedUri">The associatedUri<see cref="string?"/></param>
        /// <param name="lastResolvedIp">The lastResolvedIp<see cref="string?"/></param>
        /// <param name="statusCode">The statusCode<see cref="int?"/></param>
        /// <param name="statusText">The statusText<see cref="string?"/></param>
        /// <param name="expiresAt">The expiresAt<see cref="DateTime?"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int SaveOrUpdateRuntimeRegistrationExternal(
            int providerAccountId,
            string providerName,
            string aorUri,
            string? contactUri,
            string? remoteEndPoint,
            string? transport,
            string? requestUri,
            string? callId,
            string? serviceRoute,
            string? associatedUri,
            string? lastResolvedIp,
            int? statusCode,
            string? statusText,
            DateTime? expiresAt,
            bool isRegistered,
            string? errorText = null) {

            const string sql = @"
MERGE dbo.TK_RuntimeRegistration AS target
USING (SELECT @RegistrationType AS RegistrationType, @SourceID AS SourceID) AS source
ON target.RegistrationType = source.RegistrationType AND target.SourceID = source.SourceID
WHEN MATCHED THEN
    UPDATE SET
        SourceType = 'ProviderAccount',
        ProviderAccountID = @ProviderAccountID,
        UserID = NULL,
        InternalStateID = NULL,
        DisplayName = @DisplayName,
        AorUri = @AorUri,
        ContactURI = @ContactURI,
        RemoteEndPoint = @RemoteEndPoint,
        Transport = @Transport,
        RequestUri = @RequestUri,
        CallID = @CallID,
        ServiceRoute = @ServiceRoute,
        RouteSet = @ServiceRoute,
        AssociatedUri = @AssociatedUri,
        LastResolvedIP = @LastResolvedIP,
        StatusCode = @StatusCode,
        StatusText = @StatusText,
        ExpiresAt = @ExpiresAt,
        LastSeen = SYSDATETIME(),
        IsRegistered = @IsRegistered,
        IsReachable = @IsRegistered,
        LastError = @LastError,
        UpdatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (GUID, RegistrationType, SourceType, SourceID, ProviderAccountID, DisplayName, AorUri, ContactURI, RemoteEndPoint, Transport, RequestUri, CallID, ServiceRoute, RouteSet, AssociatedUri, LastResolvedIP, StatusCode, StatusText, ExpiresAt, LastSeen, IsRegistered, IsReachable, LastError, CreatedAt, UpdatedAt)
    VALUES (NEWID(), @RegistrationType, 'ProviderAccount', @SourceID, @ProviderAccountID, @DisplayName, @AorUri, @ContactURI, @RemoteEndPoint, @Transport, @RequestUri, @CallID, @ServiceRoute, @ServiceRoute, @AssociatedUri, @LastResolvedIP, @StatusCode, @StatusText, @ExpiresAt, SYSDATETIME(), @IsRegistered, @IsRegistered, @LastError, SYSDATETIME(), SYSDATETIME())
OUTPUT INSERTED.ID;";

            using SqlCommand cmd = new SqlCommand(sql, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@RegistrationType", "External");
            cmd.Parameters.AddWithValue("@SourceID", providerAccountId);
            cmd.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
            cmd.Parameters.AddWithValue("@DisplayName", providerName ?? string.Empty);
            cmd.Parameters.AddWithValue("@AorUri", aorUri ?? string.Empty);
            cmd.Parameters.AddWithValue("@ContactURI", (object?)contactUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RemoteEndPoint", (object?)remoteEndPoint ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Transport", (object?)transport ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RequestUri", (object?)requestUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CallID", (object?)callId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ServiceRoute", (object?)serviceRoute ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AssociatedUri", (object?)associatedUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastResolvedIP", (object?)lastResolvedIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StatusCode", (object?)statusCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StatusText", (object?)statusText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExpiresAt", (object?)expiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsRegistered", isRegistered);
            cmd.Parameters.AddWithValue("@LastError", (object?)errorText ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// The DeactivateInternalRegistrationStateByCallId
        /// </summary>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DeactivateInternalRegistrationStateByCallId(string callId) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_InternalRegistrationState
SET IsRegistered = 0,
    IsReachable = 0,
    UpdatedAt = SYSDATETIME(),
    LastSeen = SYSDATETIME()
WHERE CallID = @CallID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@CallID", callId ?? string.Empty);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The DeactivateRuntimeRegistrationInternalByCallId
        /// </summary>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DeactivateRuntimeRegistrationInternalByCallId(string callId) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_RuntimeRegistration
SET IsRegistered = 0,
    IsReachable = 0,
    UpdatedAt = SYSDATETIME(),
    LastSeen = SYSDATETIME()
WHERE RegistrationType = 'Internal'
  AND CallID = @CallID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@CallID", callId ?? string.Empty);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The CleanupExpiredInternalRegistrationStates
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int CleanupExpiredInternalRegistrationStates() {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_InternalRegistrationState
SET IsRegistered = 0,
    IsReachable = 0,
    UpdatedAt = SYSDATETIME()
WHERE IsRegistered = 1
  AND ExpiresAt <= SYSDATETIME();", TKSQL_ConnectionClass.GetConnection());
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The CleanupExpiredRuntimeRegistrations
        /// </summary>
        /// <param name="registrationType">The registrationType<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int CleanupExpiredRuntimeRegistrations(string registrationType) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_RuntimeRegistration
SET IsRegistered = 0,
    IsReachable = 0,
    UpdatedAt = SYSDATETIME()
WHERE RegistrationType = @RegistrationType
  AND IsRegistered = 1
  AND ExpiresAt IS NOT NULL
  AND ExpiresAt <= SYSDATETIME();", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@RegistrationType", registrationType ?? string.Empty);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The SaveOrUpdateRuntimeCall
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="bridgeId">The bridgeId<see cref="Guid?"/></param>
        /// <param name="callDirection">The callDirection<see cref="string"/></param>
        /// <param name="callType">The callType<see cref="string"/></param>
        /// <param name="routeKind">The routeKind<see cref="string?"/></param>
        /// <param name="status">The status<see cref="string"/></param>
        /// <param name="fromUserId">The fromUserId<see cref="int?"/></param>
        /// <param name="toUserId">The toUserId<see cref="int?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string?"/></param>
        /// <param name="toNumber">The toNumber<see cref="string?"/></param>
        /// <param name="displayFrom">The displayFrom<see cref="string?"/></param>
        /// <param name="displayTo">The displayTo<see cref="string?"/></param>
        /// <param name="internalNumber">The internalNumber<see cref="string?"/></param>
        /// <param name="externalNumber">The externalNumber<see cref="string?"/></param>
        /// <param name="outboundExternalNumber">The outboundExternalNumber<see cref="string?"/></param>
        /// <param name="providerAccountId">The providerAccountId<see cref="int?"/></param>
        /// <param name="gsmStatusId">The gsmStatusId<see cref="int?"/></param>
        /// <param name="gsmModemId">The gsmModemId<see cref="int?"/></param>
        /// <param name="gsmSimId">The gsmSimId<see cref="int?"/></param>
        /// <param name="legACallId">The legACallId<see cref="string?"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <param name="legDCallId">The legDCallId<see cref="string?"/></param>
        /// <param name="legAStatus">The legAStatus<see cref="string?"/></param>
        /// <param name="legBStatus">The legBStatus<see cref="string?"/></param>
        /// <param name="legDStatus">The legDStatus<see cref="string?"/></param>
        /// <param name="contactUriA">The contactUriA<see cref="string?"/></param>
        /// <param name="contactUriB">The contactUriB<see cref="string?"/></param>
        /// <param name="remoteEndPointA">The remoteEndPointA<see cref="string?"/></param>
        /// <param name="remoteEndPointB">The remoteEndPointB<see cref="string?"/></param>
        /// <param name="canHangup">The canHangup<see cref="bool"/></param>
        /// <param name="canTransfer">The canTransfer<see cref="bool"/></param>
        /// <param name="transferTarget">The transferTarget<see cref="string?"/></param>
        /// <param name="startedAt">The startedAt<see cref="DateTime?"/></param>
        /// <param name="answeredAt">The answeredAt<see cref="DateTime?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int SaveOrUpdateRuntimeCall(
            string sessionId,
            Guid? bridgeId,
            string callDirection,
            string callType,
            string? routeKind,
            string status,
            int? fromUserId,
            int? toUserId,
            string? fromNumber,
            string? toNumber,
            string? displayFrom,
            string? displayTo,
            string? internalNumber,
            string? externalNumber,
            string? outboundExternalNumber,
            int? providerAccountId,
            int? gsmStatusId,
            int? gsmModemId,
            int? gsmSimId,
            string? legACallId,
            string? legBCallId,
            string? legDCallId,
            string? legAStatus,
            string? legBStatus,
            string? legDStatus,
            string? contactUriA,
            string? contactUriB,
            string? remoteEndPointA,
            string? remoteEndPointB,
            bool canHangup,
            bool canTransfer,
            string? transferTarget,
            DateTime? startedAt,
            DateTime? answeredAt) {

            const string sql = @"
MERGE dbo.TK_RuntimeCall AS target
USING (SELECT @SessionID AS SessionID) AS source
ON target.SessionID = source.SessionID
WHEN MATCHED THEN
    UPDATE SET
        BridgeID = @BridgeID,
        CallDirection = @CallDirection,
        CallType = @CallType,
        RouteKind = @RouteKind,
        Status = @Status,
        FromUserID = @FromUserID,
        ToUserID = @ToUserID,
        FromNumber = @FromNumber,
        ToNumber = @ToNumber,
        DisplayFrom = @DisplayFrom,
        DisplayTo = @DisplayTo,
        InternalNumber = @InternalNumber,
        ExternalNumber = @ExternalNumber,
        OutboundExternalNumber = @OutboundExternalNumber,
        ProviderAccountID = @ProviderAccountID,
        GsmStatusID = @GsmStatusID,
        GsmModemID = @GsmModemID,
        GsmSimID = @GsmSimID,
        LegACallID = @LegACallID,
        LegBCallID = @LegBCallID,
        LegDCallID = @LegDCallID,
        LegAStatus = @LegAStatus,
        LegBStatus = @LegBStatus,
        LegDStatus = @LegDStatus,
        ContactUriA = @ContactUriA,
        ContactUriB = @ContactUriB,
        RemoteEndPointA = @RemoteEndPointA,
        RemoteEndPointB = @RemoteEndPointB,
        CanHangup = @CanHangup,
        CanTransfer = @CanTransfer,
        TransferTarget = @TransferTarget,
        StartedAt = COALESCE(@StartedAt, target.StartedAt),
        AnsweredAt = COALESCE(@AnsweredAt, target.AnsweredAt),
        LastUpdatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (
        SessionID, BridgeID, CallDirection, CallType, RouteKind, Status,
        FromUserID, ToUserID, FromNumber, ToNumber, DisplayFrom, DisplayTo,
        InternalNumber, ExternalNumber, OutboundExternalNumber,
        ProviderAccountID, GsmStatusID, GsmModemID, GsmSimID,
        LegACallID, LegBCallID, LegDCallID,
        LegAStatus, LegBStatus, LegDStatus,
        ContactUriA, ContactUriB, RemoteEndPointA, RemoteEndPointB,
        CanHangup, CanTransfer, TransferTarget, StartedAt, AnsweredAt, LastUpdatedAt
    )
    VALUES (
        @SessionID, @BridgeID, @CallDirection, @CallType, @RouteKind, @Status,
        @FromUserID, @ToUserID, @FromNumber, @ToNumber, @DisplayFrom, @DisplayTo,
        @InternalNumber, @ExternalNumber, @OutboundExternalNumber,
        @ProviderAccountID, @GsmStatusID, @GsmModemID, @GsmSimID,
        @LegACallID, @LegBCallID, @LegDCallID,
        @LegAStatus, @LegBStatus, @LegDStatus,
        @ContactUriA, @ContactUriB, @RemoteEndPointA, @RemoteEndPointB,
        @CanHangup, @CanTransfer, @TransferTarget, COALESCE(@StartedAt, SYSDATETIME()), @AnsweredAt, SYSDATETIME()
    );";

            using SqlCommand cmd = new SqlCommand(sql, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SessionID", sessionId ?? string.Empty);
            cmd.Parameters.AddWithValue("@BridgeID", (object?)bridgeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CallDirection", callDirection ?? "Internal");
            cmd.Parameters.AddWithValue("@CallType", callType ?? "Internal");
            cmd.Parameters.AddWithValue("@RouteKind", (object?)routeKind ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", status ?? "Trying");
            cmd.Parameters.AddWithValue("@FromUserID", (object?)fromUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ToUserID", (object?)toUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FromNumber", (object?)fromNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ToNumber", (object?)toNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DisplayFrom", (object?)displayFrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DisplayTo", (object?)displayTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InternalNumber", (object?)internalNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExternalNumber", (object?)externalNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OutboundExternalNumber", (object?)outboundExternalNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProviderAccountID", (object?)providerAccountId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GsmStatusID", (object?)gsmStatusId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GsmModemID", (object?)gsmModemId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GsmSimID", (object?)gsmSimId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegACallID", (object?)legACallId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegBCallID", (object?)legBCallId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegDCallID", (object?)legDCallId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegAStatus", (object?)legAStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegBStatus", (object?)legBStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LegDStatus", (object?)legDStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ContactUriA", (object?)contactUriA ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ContactUriB", (object?)contactUriB ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RemoteEndPointA", (object?)remoteEndPointA ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RemoteEndPointB", (object?)remoteEndPointB ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CanHangup", canHangup);
            cmd.Parameters.AddWithValue("@CanTransfer", canTransfer);
            cmd.Parameters.AddWithValue("@TransferTarget", (object?)transferTarget ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StartedAt", (object?)startedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AnsweredAt", (object?)answeredAt ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The UpdateRuntimeCallOutboundInvite
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateRuntimeCallOutboundInvite(string sessionId, string? legBCallId) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_RuntimeCall
SET LegBCallID = COALESCE(@LegBCallID, LegBCallID),
    LegBStatus = CASE WHEN LegBCallID IS NULL AND @LegBCallID IS NOT NULL THEN N'Trying' ELSE COALESCE(LegBStatus, N'Trying') END,
    LastUpdatedAt = SYSDATETIME()
WHERE SessionID = @SessionID OR LegACallID = @SessionID OR LegBCallID = @SessionID OR LegDCallID = @SessionID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SessionID", sessionId ?? string.Empty);
            cmd.Parameters.AddWithValue("@LegBCallID", (object?)legBCallId ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The MarkRuntimeCallRinging
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int MarkRuntimeCallRinging(string sessionId) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_RuntimeCall
SET Status = CASE WHEN Status = N'Answered' THEN Status ELSE N'Ringing' END,
    LegAStatus = CASE WHEN LegACallID IS NOT NULL AND LegAStatus <> N'Answered' THEN N'Ringing' ELSE LegAStatus END,
    LegBStatus = CASE WHEN LegBCallID IS NOT NULL AND LegBStatus <> N'Answered' THEN N'Ringing' ELSE LegBStatus END,
    LegDStatus = CASE WHEN LegDCallID IS NOT NULL AND LegDStatus <> N'Answered' THEN N'Ringing' ELSE LegDStatus END,
    LastUpdatedAt = SYSDATETIME()
WHERE SessionID = @SessionID OR LegACallID = @SessionID OR LegBCallID = @SessionID OR LegDCallID = @SessionID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SessionID", sessionId ?? string.Empty);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The MarkRuntimeCallAnswered
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="answeredAt">The answeredAt<see cref="DateTime"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int MarkRuntimeCallAnswered(string sessionId, DateTime answeredAt) {
            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_RuntimeCall
SET Status = N'Answered',
    AnsweredAt = COALESCE(@AnsweredAt, AnsweredAt),
    LegAStatus = CASE WHEN LegACallID IS NOT NULL THEN N'Answered' ELSE LegAStatus END,
    LegBStatus = CASE WHEN LegBCallID IS NOT NULL THEN N'Answered' ELSE LegBStatus END,
    LegDStatus = CASE WHEN LegDCallID IS NOT NULL THEN N'Answered' ELSE LegDStatus END,
    LastUpdatedAt = SYSDATETIME()
WHERE SessionID = @SessionID OR LegACallID = @SessionID OR LegBCallID = @SessionID OR LegDCallID = @SessionID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SessionID", sessionId ?? string.Empty);
            cmd.Parameters.AddWithValue("@AnsweredAt", answeredAt);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The DeleteRuntimeCall
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DeleteRuntimeCall(string sessionId) {
            using SqlCommand cmd = new SqlCommand(@"
DELETE FROM dbo.TK_RuntimeCall
WHERE SessionID = @SessionID OR LegACallID = @SessionID OR LegBCallID = @SessionID OR LegDCallID = @SessionID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SessionID", sessionId ?? string.Empty);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// The IsOutboundExternalNumberBusy
        /// </summary>
        /// <param name="numberValue">The numberValue<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool IsOutboundExternalNumberBusy(string numberValue) {
            string normalized = TKSipExt_Routing.NormalizeExternalNumber(numberValue);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return false;
            }

            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.TK_RuntimeCall
WHERE OutboundExternalNumber = @OutboundExternalNumber
  AND Status IN (N'Trying', N'Ringing', N'Answered', N'Holding', N'Transferring');", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@OutboundExternalNumber", normalized);
            object? result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value;
        }

        /// <summary>
        /// The GetRuntimeCallMonitoringTable
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable GetRuntimeCallMonitoringTable() {
            const string query = @"
IF OBJECT_ID('dbo.TK_RuntimeCall', 'U') IS NULL
BEGIN
    SELECT
        CAST(NULL AS nvarchar(100)) AS [SessionID],
        CAST(NULL AS nvarchar(32)) AS [Status],
        CAST(NULL AS nvarchar(20)) AS [RouteKind],
        CAST(NULL AS nvarchar(16)) AS [CallDirection],
        CAST(NULL AS nvarchar(50)) AS [FromNumber],
        CAST(NULL AS nvarchar(50)) AS [ToNumber],
        CAST(NULL AS nvarchar(50)) AS [OutboundExternalNumber],
        CAST(NULL AS nvarchar(100)) AS [DisplayFrom],
        CAST(NULL AS nvarchar(100)) AS [DisplayTo],
        CAST(NULL AS datetime2(7)) AS [StartedAt],
        CAST(NULL AS datetime2(7)) AS [AnsweredAt],
        CAST(NULL AS datetime2(7)) AS [LastUpdatedAt],
        CAST(NULL AS bit) AS [CanHangup],
        CAST(NULL AS bit) AS [CanTransfer],
        CAST(NULL AS nvarchar(255)) AS [Info]
    WHERE 1 = 0;
END
ELSE
BEGIN
    SELECT
        rc.SessionID,
        rc.Status,
        rc.RouteKind,
        rc.CallDirection,
        rc.FromNumber,
        rc.ToNumber,
        rc.OutboundExternalNumber,
        COALESCE(NULLIF(rc.DisplayFrom, N''), NULLIF(rc.FromNumber, N''), N'-') AS [DisplayFrom],
        COALESCE(NULLIF(rc.DisplayTo, N''), NULLIF(rc.ToNumber, N''), N'-') AS [DisplayTo],
        rc.StartedAt,
        rc.AnsweredAt,
        rc.LastUpdatedAt,
        rc.CanHangup,
        rc.CanTransfer,
        CONCAT(
            N'Provider=', COALESCE(CONVERT(nvarchar(20), rc.ProviderAccountID), N'-'),
            N' | GSMStatus=', COALESCE(CONVERT(nvarchar(20), rc.GsmStatusID), N'-'),
            N' | LegA=', COALESCE(rc.LegACallID, N'-'),
            N' | LegB=', COALESCE(rc.LegBCallID, N'-'),
            N' | LegD=', COALESCE(rc.LegDCallID, N'-')
        ) AS [Info]
    FROM dbo.TK_RuntimeCall rc
    ORDER BY rc.StartedAt ASC, rc.SessionID ASC;
END;";
            using SqlDataAdapter adapter = new SqlDataAdapter(query, TKSQL_ConnectionClass.GetConnection());
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        /// <summary>
        /// The GetActiveRuntimeCalls
        /// </summary>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetActiveRuntimeCalls() {
            const string query = @"
SELECT *
FROM dbo.TK_RuntimeCall
ORDER BY StartedAt ASC, ID ASC;";
            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The GetRuntimeCallBySessionId
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetRuntimeCallBySessionId(string sessionId) {
            const string query = @"
SELECT TOP 1 *
FROM dbo.TK_RuntimeCall
WHERE SessionID = @SessionID OR LegACallID = @SessionID OR LegBCallID = @SessionID OR LegDCallID = @SessionID
ORDER BY ID DESC;";
            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@SessionID", sessionId ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetNextId
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int GetNextId(string tableName) {
            string safeTableName = tableName switch {
                "TK_SIP_Registration" => tableName,
                "TK_SIP_CallLog" => tableName,
                _ => throw new InvalidOperationException($"UnzulÃƒÂ¤ssiger Tabellenname: {tableName}")
            };

            using SqlCommand cmd = new SqlCommand($@"
SELECT ISNULL(MAX(ID), 0) + 1
FROM dbo.[{safeTableName}];", TKSQL_ConnectionClass.GetConnection());

            object? result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// The GetLatestGsmPersistedPortsByPnpDeviceId
        /// </summary>
        /// <param name="pnpDeviceId">The pnpDeviceId<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetLatestGsmPersistedPortsByPnpDeviceId(string pnpDeviceId) {
            const string query = @"
SELECT TOP 1
    ms.ComPortAT,
    ms.ComPortAudio,
    ms.ComPortDiag,
    ms.ComPortNMEA
FROM dbo.TK_GSM_Modems m
INNER JOIN dbo.TK_GSM_ModemSimStatus ms
    ON ms.ModemID = m.ID
WHERE m.PNPDeviceID = @PNPDeviceID
ORDER BY ms.UpdatedAt DESC, ms.ID DESC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@PNPDeviceID", pnpDeviceId ?? string.Empty);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetGsmGuidById
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <param name="id">The id<see cref="int"/></param>
        /// <returns>The <see cref="Guid"/></returns>
        public Guid GetGsmGuidById(string tableName, int id) {
            if (id <= 0)
                throw new ArgumentException("ID muss grÃƒÂ¶ÃƒÅ¸er 0 sein.", nameof(id));

            string normalizedTableName = NormalizeGsmGuidTableName(tableName);

            using SqlCommand cmd = new SqlCommand($@"
SELECT TOP 1 [GUID]
FROM [dbo].[{normalizedTableName}]
WHERE [ID] = @ID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ID", id);

            object? result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                throw new Exception($"GUID konnte nicht geladen werden | Tabelle={normalizedTableName} | ID={id}");

            return (Guid)result;
        }

        /// <summary>
        /// The BuildGsmModemSimStatusGuid
        /// </summary>
        /// <param name="modemId">The modemId<see cref="int"/></param>
        /// <param name="simId">The simId<see cref="int?"/></param>
        /// <returns>The <see cref="Guid"/></returns>
        public Guid BuildGsmModemSimStatusGuid(int modemId, int? simId) {
            if (modemId <= 0)
                throw new ArgumentException("modemId muss grÃƒÂ¶ÃƒÅ¸er 0 sein.", nameof(modemId));

            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    [dbo].[TK_GUID](
        CONCAT(
            CONVERT(nvarchar(36), m.[GUID]),
            N'|',
            ISNULL(CONVERT(nvarchar(36), s.[GUID]), N'')
        )
    )
FROM [dbo].[TK_GSM_Modems] m
LEFT JOIN [dbo].[TK_GSM_Sim] s
    ON s.[ID] = @SimID
WHERE m.[ID] = @ModemID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ModemID", modemId);
            cmd.Parameters.AddWithValue("@SimID", simId.HasValue ? simId.Value : (object)DBNull.Value);

            object? result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                throw new Exception($"ModemSimStatus-GUID konnte nicht erzeugt werden | ModemID={modemId} | SimID={(simId.HasValue ? simId.Value.ToString() : "NULL")}");

            return (Guid)result;
        }

        /// <summary>
        /// The GetActiveGsmRuntimeDeviceByStatusId
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetActiveGsmRuntimeDeviceByStatusId(int statusId) {
            const string query = @"
SELECT TOP 1
    ms.ID,
    ms.ModemID,
    ms.SimID,
    ms.ComPortAT,
    ms.ComPortAudio,
    ms.ComPortDiag,
    ms.ComPortNMEA,
    ms.IsPresent,
    ms.IsSimInserted,
    ms.IsRegistered,
    ms.SignalStrength,
    ms.LastSeen,
    ms.LastError,
    m.ModemName,
    m.IMEI,
    m.PNPDeviceID,
    s.Telefonnummer,
    s.ProviderName,
    s.ICCID,
    s.IMSI
FROM dbo.TK_GSM_ModemSimStatus ms
INNER JOIN dbo.TK_GSM_Modems m
    ON m.ID = ms.ModemID
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
WHERE ms.ID = @StatusID
  AND ISNULL(ms.IsPresent, 0) = 1
  AND ISNULL(m.Active, 0) = 1
ORDER BY ms.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@StatusID", statusId);
            return ExecuteSingleRow(cmd);
        }

        /// <summary>
        /// The GetActiveGsmRuntimeDevices
        /// </summary>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        public List<DataRow> GetActiveGsmRuntimeDevices() {
            const string query = @"
SELECT
    ms.ID,
    ms.ModemID,
    ms.SimID,
    ms.ComPortAT,
    ms.ComPortAudio,
    ms.ComPortDiag,
    ms.ComPortNMEA,
    ms.IsPresent,
    ms.IsSimInserted,
    ms.IsRegistered,
    ms.SignalStrength,
    ms.LastSeen,
    ms.LastError,
    m.ModemName,
    m.IMEI,
    m.PNPDeviceID,
    s.Telefonnummer,
    s.ProviderName,
    s.ICCID,
    s.IMSI
FROM dbo.TK_GSM_ModemSimStatus ms
INNER JOIN dbo.TK_GSM_Modems m
    ON m.ID = ms.ModemID
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
WHERE ISNULL(ms.IsPresent, 0) = 1
  AND ISNULL(m.Active, 0) = 1
ORDER BY ms.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteRows(cmd);
        }

        /// <summary>
        /// The NormalizeGsmGuidTableName
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeGsmGuidTableName(string tableName) {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tabellenname ist leer.", nameof(tableName));

            return tableName.Trim() switch {
                "TK_GSM_Modems" => "TK_GSM_Modems",
                "TK_GSM_Sim" => "TK_GSM_Sim",
                "TK_GSM_ModemSimStatus" => "TK_GSM_ModemSimStatus",
                _ => throw new ArgumentException($"Nicht erlaubte GSM-GUID-Tabelle: {tableName}", nameof(tableName))
            };
        }

        /// <summary>
        /// The ExecuteTable
        /// </summary>
        /// <param name="command">The command<see cref="SqlCommand"/></param>
        /// <returns>The <see cref="DataTable"/></returns>
        private static DataTable ExecuteTable(SqlCommand command) {
            using SqlDataAdapter adapter = new SqlDataAdapter(command);
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        /// <summary>
        /// The DbRequiredString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="fallback">The fallback<see cref="string"/></param>
        /// <returns>The <see cref="object"/></returns>
        private static object DbRequiredString(DataRow row, string columnName, string fallback) {
            string value = row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToString(row[columnName])?.Trim() ?? string.Empty
                : string.Empty;

            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        /// <summary>
        /// The DbString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="object"/></returns>
        private static object DbString(DataRow row, string column) {
            string value = row.Table.Columns.Contains(column) && row[column] != DBNull.Value
                ? Convert.ToString(row[column])?.Trim() ?? string.Empty
                : string.Empty;
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        /// <summary>
        /// The DbInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="object"/></returns>
        private static object DbInt(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return DBNull.Value;
            string value = Convert.ToString(row[column])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// The DbIntRequired
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int DbIntRequired(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) {
                throw new InvalidOperationException($"Pflichtfeld {column} fehlt.");
            }

            return Convert.ToInt32(row[column]);
        }

        /// <summary>
        /// The DbBool
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="object"/></returns>
        private static object DbBool(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return DBNull.Value;
            string value = Convert.ToString(row[column])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
            return Convert.ToBoolean(row[column]);
        }

        /// <summary>
        /// The OnlyDigits
        /// </summary>
        /// <param name="input">The input<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string OnlyDigits(string input) {
            string value = input ?? string.Empty;
            char[] buffer = new char[value.Length];
            int index = 0;
            foreach (char c in value) {
                if (char.IsDigit(c)) {
                    buffer[index++] = c;
                }
            }

            return new string(buffer, 0, index);
        }


        /// <summary>
        /// LÃƒÂ¤dt die GSM-Zeilen fÃƒÂ¼r das Gateway-Dashboard.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadGsmGatewayDashboardRowsTable() {
            const string query = @"
SELECT
    ms.ID,
    COALESCE(NULLIF(s.Telefonnummer, ''), '-') AS Telefonnummer,
    COALESCE(NULLIF(m.IMEI, ''), '-') AS IMEI,
    ms.LastSeen
FROM dbo.TK_GSM_ModemSimStatus ms
LEFT JOIN dbo.TK_GSM_Sim s
    ON s.ID = ms.SimID
LEFT JOIN dbo.TK_GSM_Modems m
    ON m.ID = ms.ModemID
ORDER BY
    CASE WHEN ms.LastSeen IS NULL THEN 1 ELSE 0 END,
    ms.LastSeen DESC,
    ms.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt ProviderAccounts fÃƒÂ¼r die Provider-GUI.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadProviderAccountsGuiTable() {
            using SqlCommand cmd = new SqlCommand(ProviderAccountsGuiSelectSql, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// Speichert ProviderAccounts aus der Provider-GUI.
        /// </summary>
        /// <param name="providerTable">The providerTable<see cref="DataTable"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int SaveProviderAccountsGuiTable(DataTable providerTable) {
            using SqlDataAdapter adapter = new SqlDataAdapter(ProviderAccountsGuiSelectSql, TKSQL_ConnectionClass.GetConnection());
            using SqlCommandBuilder builder = new SqlCommandBuilder(adapter);
            return adapter.Update(providerTable);
        }

        private const string ProviderAccountsGuiSelectSql = @"
SELECT
    ID,
    ProviderName,
    SipAuthUser,
    SIPUser,
    SIPPasswort,
    SIPServer,
    SIPRegistrar,
    SIPProxy,
    SIPOutboundProxy,
    SIPRealm,
    SIPSTUNServer,
    SIPProtokoll,
    SIPRTPPort,
    SIPPort,
    SRTP,
    SIPNr,
    Active,
    CreatedAt,
    UpdatedAt
FROM dbo.TK_ProviderAccounts
ORDER BY ID;";

        /// <summary>
        /// LÃƒÂ¤dt ProviderAccounts fÃƒÂ¼r die externe Provider-Registrierung.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadProviderAccountsForRegistration() {
            const string query = @"
SELECT
    ID,
    ProviderName,
    SipAuthUser,
    SIPUser,
    SIPPasswort,
    SIPServer,
    SIPRegistrar,
    SIPProxy,
    SIPOutboundProxy,
    SIPRealm,
    SIPProtokoll,
    SIPPort,
    Active
FROM dbo.TK_ProviderAccounts
ORDER BY ID;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// Speichert den externen Provider-Registrierungsstatus.
        /// </summary>
        public int SaveProviderRegistrationState(
            int providerAccountId,
            bool isRegistered,
            DateTime lastRegisterAt,
            DateTime? expiresAt,
            string? lastRemoteEndPoint,
            string? lastResolvedIp,
            string? lastTransport,
            string? lastRequestUri,
            string? serviceRoute,
            string? associatedUri,
            string? lastCallId,
            int? lastStatusCode,
            string? lastStatusText,
            string? lastError) {
            using SqlCommand cmd = new SqlCommand(@"
MERGE dbo.TK_ProviderRegistrationState AS target
USING (SELECT @ProviderAccountID AS ProviderAccountID) AS source
ON target.ProviderAccountID = source.ProviderAccountID
WHEN MATCHED THEN
    UPDATE SET
        IsRegistered = @IsRegistered,
        LastRegisterAt = @LastRegisterAt,
        ExpiresAt = @ExpiresAt,
        LastRemoteEndPoint = @LastRemoteEndPoint,
        LastResolvedIP = @LastResolvedIP,
        LastTransport = @LastTransport,
        LastRequestUri = @LastRequestUri,
        ServiceRoute = @ServiceRoute,
        RouteSet = @RouteSet,
        AssociatedUri = @AssociatedUri,
        LastCallID = @LastCallID,
        LastStatusCode = @LastStatusCode,
        LastStatusText = @LastStatusText,
        LastError = @LastError,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ProviderAccountID, IsRegistered, LastRegisterAt, ExpiresAt, LastRemoteEndPoint, LastResolvedIP, LastTransport, LastRequestUri, ServiceRoute, RouteSet, AssociatedUri, LastCallID, LastStatusCode, LastStatusText, LastError, UpdatedAt)
    VALUES (@ProviderAccountID, @IsRegistered, @LastRegisterAt, @ExpiresAt, @LastRemoteEndPoint, @LastResolvedIP, @LastTransport, @LastRequestUri, @ServiceRoute, @RouteSet, @AssociatedUri, @LastCallID, @LastStatusCode, @LastStatusText, @LastError, SYSUTCDATETIME());", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
            cmd.Parameters.AddWithValue("@IsRegistered", isRegistered);
            cmd.Parameters.AddWithValue("@LastRegisterAt", lastRegisterAt);
            cmd.Parameters.AddWithValue("@ExpiresAt", (object?)expiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastRemoteEndPoint", DbOptionalString(lastRemoteEndPoint));
            cmd.Parameters.AddWithValue("@LastResolvedIP", DbOptionalString(lastResolvedIp));
            cmd.Parameters.AddWithValue("@LastTransport", DbOptionalString(lastTransport));
            cmd.Parameters.AddWithValue("@LastRequestUri", DbOptionalString(lastRequestUri));
            cmd.Parameters.AddWithValue("@ServiceRoute", DbOptionalString(serviceRoute));
            cmd.Parameters.AddWithValue("@RouteSet", DbOptionalString(serviceRoute));
            cmd.Parameters.AddWithValue("@AssociatedUri", DbOptionalString(associatedUri));
            cmd.Parameters.AddWithValue("@LastCallID", DbOptionalString(lastCallId));
            cmd.Parameters.AddWithValue("@LastStatusCode", (object?)lastStatusCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastStatusText", DbOptionalString(lastStatusText));
            cmd.Parameters.AddWithValue("@LastError", DbOptionalString(lastError));
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Schreibt einen externen Provider-Registrierungslogeintrag.
        /// </summary>
        public int InsertProviderRegistrationLog(
            int providerAccountId,
            string eventType,
            int? statusCode,
            string? statusText,
            string? remoteEndPoint,
            string? requestUri,
            string? transport,
            string? callId,
            string? rawRequest,
            string? rawResponse,
            string? errorText) {
            using SqlCommand cmd = new SqlCommand(@"
INSERT INTO dbo.TK_ProviderRegistrationLog
(ProviderAccountID, EventType, StatusCode, StatusText, RemoteEndPoint, RequestUri, Transport, CallID, RawRequest, RawResponse, ErrorText, CreatedAt)
VALUES
(@ProviderAccountID, @EventType, @StatusCode, @StatusText, @RemoteEndPoint, @RequestUri, @Transport, @CallID, @RawRequest, @RawResponse, @ErrorText, SYSUTCDATETIME());", TKSQL_ConnectionClass.GetConnection());

            cmd.Parameters.AddWithValue("@ProviderAccountID", providerAccountId);
            cmd.Parameters.AddWithValue("@EventType", eventType ?? string.Empty);
            cmd.Parameters.AddWithValue("@StatusCode", (object?)statusCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StatusText", DbOptionalString(statusText));
            cmd.Parameters.AddWithValue("@RemoteEndPoint", DbOptionalString(remoteEndPoint));
            cmd.Parameters.AddWithValue("@RequestUri", DbOptionalString(requestUri));
            cmd.Parameters.AddWithValue("@Transport", DbOptionalString(transport));
            cmd.Parameters.AddWithValue("@CallID", DbOptionalString(callId));
            cmd.Parameters.AddWithValue("@RawRequest", DbOptionalString(rawRequest));
            cmd.Parameters.AddWithValue("@RawResponse", DbOptionalString(rawResponse));
            cmd.Parameters.AddWithValue("@ErrorText", DbOptionalString(errorText));
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// LÃƒÂ¤dt die SIP-CallLog-Tabelle fÃƒÂ¼r die GUI.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadSipCallLogTable() {
            const string query = @"
SELECT *
FROM [TK-Server].[dbo].[TK_SIP_CallLog];";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt externe Nummern fÃƒÂ¼r die Routing-GUI.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadExternalNumbersLookupTable() {
            const string query = @"
SELECT
    en.ID,
    en.NumberValue,
    en.DisplayName,
    en.IsOnline,
    en.IsSelectable,
    en.Active,
    en.SourceType,
    en.SourceID
FROM dbo.TK_ExternalNumbers en
ORDER BY en.NumberValue ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt interne SIP-User fÃƒÂ¼r die Routing-GUI.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadSipUsersLookupForRoutingTable() {
            const string query = @"
SELECT
    su.ID,
    su.Username,
    su.Telefonnummer,
    su.DisplayName,
    su.Active,
    su.CanDialExternal,
    su.CanReceiveExternal,
    su.UserType
FROM dbo.TK_SIP_User su
WHERE ISNULL(su.Active, 0) = 1
ORDER BY su.Telefonnummer ASC, su.Username ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt externe Nummernrouten fÃƒÂ¼r die Routing-GUI.
        /// </summary>
        /// <param name="includeInactive">The includeInactive<see cref="bool"/></param>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadExternalNumberRoutingTable(bool includeInactive) {
            string activeWhere = includeInactive ? string.Empty : "WHERE r.Active = 1";
            string query = $@"
SELECT
    r.*,
    en.Active AS ExternalNumberActive,
    en.IsSelectable AS ExternalNumberSelectable,
    en.SourceType AS ExternalNumberSourceType,
    en.SourceID AS ExternalNumberSourceID
FROM dbo.TK_ExternalNumberRouting r
INNER JOIN dbo.TK_ExternalNumbers en ON en.ID = r.ExternalNumberID
{activeWhere}
ORDER BY r.InternalNumberID ASC, r.Priority ASC, r.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// Speichert Ãƒâ€žnderungen der Routing-GUI in TK_ExternalNumberRouting.
        /// </summary>
        public void SaveExternalNumberRoutingChanges(DataTable routingTable, string internalColumnName, bool hasInterneNrColumn, bool hasExterneNrColumn) {
            SqlConnection con = TKSQL_ConnectionClass.GetConnection();
            using SqlTransaction tx = con.BeginTransaction();

            try {
                bool hasRestorePriority = routingTable.Columns.Contains("RestorePriority") && SqlColumnExists(con, tx, "TK_ExternalNumberRouting", "RestorePriority");
                List<int> affectedInternalIds = new List<int>();

                foreach (DataRow row in routingTable.Rows.Cast<DataRow>().Where(r => r.RowState == DataRowState.Added)) {
                    PrepareExternalRoutingRowForSave(row, hasRestorePriority);
                    ReserveActiveExternalRoutingPriority(con, tx, row, internalColumnName, 0, hasRestorePriority);
                    InsertExternalNumberRoutingRow(con, tx, row, internalColumnName, hasInterneNrColumn, hasExterneNrColumn);
                    UpdateExternalNumberStateFromRoutingRow(con, tx, row);
                    AddAffectedInternalNumberId(row, internalColumnName, affectedInternalIds);
                }

                foreach (DataRow row in routingTable.Rows.Cast<DataRow>().Where(r => r.RowState == DataRowState.Modified)) {
                    PrepareExternalRoutingRowForSave(row, hasRestorePriority);
                    ReserveActiveExternalRoutingPriority(con, tx, row, internalColumnName, Convert.ToInt32(row["ID"]), hasRestorePriority);
                    UpdateExternalNumberRoutingRow(con, tx, row, internalColumnName, hasInterneNrColumn, hasExterneNrColumn);
                    UpdateExternalNumberStateFromRoutingRow(con, tx, row);
                    AddAffectedInternalNumberId(row, internalColumnName, affectedInternalIds);
                    AddAffectedInternalNumberId(row, internalColumnName, affectedInternalIds, DataRowVersion.Original);
                }

                foreach (DataRow row in routingTable.Rows.Cast<DataRow>().Where(r => r.RowState == DataRowState.Deleted)) {
                    AddAffectedInternalNumberId(row, internalColumnName, affectedInternalIds, DataRowVersion.Original);
                    DeleteExternalNumberRoutingRow(con, tx, row);
                }

                NormalizeExternalRoutingPriorities(con, tx, affectedInternalIds.Distinct().ToList());
                EnsureUniqueExternalRoutingPriorities(con, tx);
                tx.Commit();
            } catch {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// LÃƒÂ¤dt ServiceLegs fÃƒÂ¼r die GUI.
        /// </summary>
        /// <param name="onlyActive">The onlyActive<see cref="bool"/></param>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadServiceLegsTable(bool onlyActive) {
            string whereClause = onlyActive ? "WHERE Active = 1" : string.Empty;
            string query = $@"
SELECT
    ID,
    InternalNumber,
    DisplayName,
    Description,
    ServiceCode,
    ServiceType,
    AutoAnswer,
    KeepSessionUntilPeerEnded,
    Active,
    SortOrder,
    PromptFile,
    RecordTemplate,
    JsonConfig,
    CreatedAt,
    UpdatedAt
FROM [TK-Server].[dbo].[TK_SIP_ServiceLeg]
{whereClause}
ORDER BY SortOrder, InternalNumber, ID;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt interne Benutzer fÃƒÂ¼r das Start-Dashboard.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadDashboardInternalUsersTable() {
            const string query = @"
SELECT
    u.Username AS [User Name],
    u.Telefonnummer AS [Interne Telefonnummer],
    u.UserType AS [Typ],
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM dbo.TK_RuntimeRegistration r
            WHERE r.UserID = u.ID
              AND r.RegistrationType = 'Internal'
              AND r.IsRegistered = 1
              AND r.IsReachable = 1
              AND (r.ExpiresAt IS NULL OR r.ExpiresAt > SYSDATETIME())
        ) THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS [IsRegistered]
FROM dbo.TK_SIP_User u
ORDER BY u.SortOrder ASC, u.Telefonnummer ASC, u.Username ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt Providerdaten fÃƒÂ¼r das Start-Dashboard.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadDashboardProvidersTable() {
            const string query = @"
SELECT
    p.ID,
    p.ProviderName AS [Provider Name],
    COALESCE(NULLIF(p.SIPNr, ''), NULLIF(en.NumberValue, ''), p.SIPUser) AS [Telefonnummer]
FROM dbo.TK_ProviderAccounts p
LEFT JOIN dbo.TK_ExternalNumbers en
    ON en.SourceType = 'ProviderAccount'
   AND en.SourceID = p.ID
ORDER BY p.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt Providerdaten inklusive Live-Registrierungsstatus fÃƒÂ¼r das Start-Dashboard.
        /// Die GUI bekommt damit eine fertige Status-Tabelle und enthÃƒÂ¤lt keine SQL- oder Registrierungslogik.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadDashboardProviderStatusTable() {
            const string query = @"
SELECT
    p.ProviderName AS [Provider Name],
    COALESCE(NULLIF(p.SIPNr, ''), NULLIF(en.NumberValue, ''), p.SIPUser) AS [Telefonnummer],
    CAST(CASE
        WHEN ISNULL(prs.IsRegistered, 0) = 1 THEN 1
        WHEN EXISTS (
            SELECT 1
            FROM dbo.TK_RuntimeRegistration rr
            WHERE rr.RegistrationType = 'External'
              AND rr.SourceType = 'ProviderAccount'
              AND rr.SourceID = p.ID
              AND rr.IsRegistered = 1
              AND rr.IsReachable = 1
              AND (rr.ExpiresAt IS NULL OR rr.ExpiresAt > SYSDATETIME())
        ) THEN 1
        ELSE 0
    END AS bit) AS [IsRegistered]
FROM dbo.TK_ProviderAccounts p
LEFT JOIN dbo.TK_ExternalNumbers en
    ON en.SourceType = 'ProviderAccount'
   AND en.SourceID = p.ID
LEFT JOIN dbo.TK_ProviderRegistrationState prs
    ON prs.ProviderAccountID = p.ID
WHERE ISNULL(p.Active, 0) = 1
ORDER BY p.ID ASC;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt VoIP-User fÃƒÂ¼r die Benutzer-GUI.
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        public DataTable LoadVoipUsersTable() {
            const string query = @"
SELECT
    ID,
    Username,
    Telefonnummer,
    [Password],
    DisplayName,
    Email,
    [Description],
    UserType,
    Active,
    CanDialInternal,
    CanDialExternal,
    CanReceiveInternal,
    CanReceiveExternal,
    SortOrder,
    CreatedAt,
    UpdatedAt
FROM dbo.TK_SIP_User
ORDER BY SortOrder, Telefonnummer, Username;";

            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            return ExecuteTable(cmd);
        }

        /// <summary>
        /// LÃƒÂ¤dt registrierte interne UserIDs.
        /// </summary>
        /// <returns>The <see cref="HashSet{int}"/></returns>
        public HashSet<int> LoadRegisteredInternalUserIds() {
            const string query = @"
SELECT DISTINCT UserID
FROM dbo.TK_RuntimeRegistration
WHERE RegistrationType = 'Internal'
  AND IsRegistered = 1
  AND IsReachable = 1
  AND (ExpiresAt IS NULL OR ExpiresAt > SYSDATETIME());";

            HashSet<int> result = new HashSet<int>();
            using SqlCommand cmd = new SqlCommand(query, TKSQL_ConnectionClass.GetConnection());
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                if (!reader.IsDBNull(0)) {
                    result.Add(reader.GetInt32(0));
                }
            }

            return result;
        }

        /// <summary>
        /// Speichert Ãƒâ€žnderungen der VoIP-User-GUI.
        /// </summary>
        /// <param name="userTable">The userTable<see cref="DataTable"/></param>
        public void SaveVoipUsers(DataTable userTable) {
            SqlConnection con = TKSQL_ConnectionClass.GetConnection();
            using SqlTransaction tx = con.BeginTransaction();

            try {
                foreach (DataRow row in userTable.Rows) {
                    if (row.RowState == DataRowState.Unchanged) {
                        continue;
                    }

                    if (row.RowState == DataRowState.Added) {
                        InsertVoipUser(con, tx, row);
                    } else if (row.RowState == DataRowState.Modified) {
                        UpdateVoipUser(con, tx, row);
                    }
                }

                tx.Commit();
            } catch {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// LÃƒÂ¶scht einen VoIP-User.
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DeleteVoipUser(int userId) {
            using SqlCommand cmd = new SqlCommand("DELETE FROM dbo.TK_SIP_User WHERE ID = @ID;", TKSQL_ConnectionClass.GetConnection());
            cmd.Parameters.AddWithValue("@ID", userId);
            return cmd.ExecuteNonQuery();
        }

        private void InsertExternalNumberRoutingRow(SqlConnection con, SqlTransaction tx, DataRow row, string internalColumnName, bool hasInterneNrColumn, bool hasExterneNrColumn) {
            List<string> columns = new List<string> {
                "ExternalNumberID",
                "TargetType",
                internalColumnName,
                "Priority",
                "Active"
            };

            if (hasInterneNrColumn) {
                columns.Add("InterneNr");
            }

            if (hasExterneNrColumn) {
                columns.Add("ExterneNr");
            }

            if (row.Table.Columns.Contains("RestorePriority")) {
                columns.Add("RestorePriority");
            }

            List<string> parameters = columns.Select(c => "@" + c).ToList();

            string sql = $@"
INSERT INTO dbo.TK_ExternalNumberRouting ({string.Join(", ", columns)}, CreatedAt, UpdatedAt)
VALUES ({string.Join(", ", parameters)}, SYSDATETIME(), SYSDATETIME());";

            using SqlCommand cmd = new SqlCommand(sql, con, tx);
            AddExternalNumberRoutingParameters(cmd, row, internalColumnName, hasInterneNrColumn, hasExterneNrColumn);
            cmd.ExecuteNonQuery();
        }

        private void UpdateExternalNumberRoutingRow(SqlConnection con, SqlTransaction tx, DataRow row, string internalColumnName, bool hasInterneNrColumn, bool hasExterneNrColumn) {
            List<string> sets = new List<string> {
                "ExternalNumberID = @ExternalNumberID",
                "TargetType = @TargetType",
                $"{internalColumnName} = @{internalColumnName}",
                "Priority = @Priority",
                "Active = @Active",
                "UpdatedAt = SYSDATETIME()"
            };

            if (hasInterneNrColumn) {
                sets.Add("InterneNr = @InterneNr");
            }

            if (hasExterneNrColumn) {
                sets.Add("ExterneNr = @ExterneNr");
            }

            if (row.Table.Columns.Contains("RestorePriority")) {
                sets.Add("RestorePriority = @RestorePriority");
            }

            string sql = $@"
UPDATE dbo.TK_ExternalNumberRouting
SET {string.Join(", ", sets)}
WHERE ID = @ID;";

            using SqlCommand cmd = new SqlCommand(sql, con, tx);
            AddExternalNumberRoutingParameters(cmd, row, internalColumnName, hasInterneNrColumn, hasExterneNrColumn);
            cmd.Parameters.AddWithValue("@ID", Convert.ToInt32(row["ID"]));
            cmd.ExecuteNonQuery();
        }

        private void UpdateExternalNumberStateFromRoutingRow(SqlConnection con, SqlTransaction tx, DataRow row) {
            if (!row.Table.Columns.Contains("ExternalNumberActive") && !row.Table.Columns.Contains("ExternalNumberSelectable")) {
                return;
            }

            int externalNumberId = Convert.ToInt32(row["ExternalNumberID"]);
            bool? numberActive = row.Table.Columns.Contains("ExternalNumberActive") && row["ExternalNumberActive"] != DBNull.Value
                ? Convert.ToBoolean(row["ExternalNumberActive"])
                : null;
            bool? numberSelectable = row.Table.Columns.Contains("ExternalNumberSelectable") && row["ExternalNumberSelectable"] != DBNull.Value
                ? Convert.ToBoolean(row["ExternalNumberSelectable"])
                : null;

            if (!numberActive.HasValue && !numberSelectable.HasValue) {
                return;
            }

            List<string> sets = new List<string> { "UpdatedAt = SYSDATETIME()" };
            if (numberActive.HasValue) {
                sets.Add("Active = @ExternalNumberActive");
            }

            if (numberSelectable.HasValue) {
                sets.Add("IsSelectable = @ExternalNumberSelectable");
            }

            using SqlCommand cmd = new SqlCommand($@"
UPDATE dbo.TK_ExternalNumbers
SET {string.Join(", ", sets)}
WHERE ID = @ExternalNumberID;", con, tx);
            cmd.Parameters.AddWithValue("@ExternalNumberID", externalNumberId);
            if (numberActive.HasValue) {
                cmd.Parameters.AddWithValue("@ExternalNumberActive", numberActive.Value);
            }

            if (numberSelectable.HasValue) {
                cmd.Parameters.AddWithValue("@ExternalNumberSelectable", numberSelectable.Value);
            }

            cmd.ExecuteNonQuery();
        }

        private void EnsureUniqueExternalRoutingPriorities(SqlConnection con, SqlTransaction tx) {
            using SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1
    InternalNumberID,
    Active,
    Priority,
    COUNT(*) AS DuplicateCount
FROM dbo.TK_ExternalNumberRouting
GROUP BY InternalNumberID, Active, Priority
HAVING COUNT(*) > 1
ORDER BY InternalNumberID ASC, Active DESC, Priority ASC;", con, tx);

            using SqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read()) {
                int internalNumberId = reader["InternalNumberID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["InternalNumberID"]);
                bool active = reader["Active"] != DBNull.Value && Convert.ToBoolean(reader["Active"]);
                int priority = reader["Priority"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Priority"]);
                int duplicateCount = reader["DuplicateCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DuplicateCount"]);
                throw new InvalidOperationException($"Routing-PrioritÃƒÂ¤t doppelt belegt | InternalNumberID={internalNumberId} | Active={active} | Priority={priority} | Count={duplicateCount}");
            }
        }

        private static bool SqlColumnExists(SqlConnection con, SqlTransaction tx, string tableName, string columnName) {
            using SqlCommand cmd = new SqlCommand(@"
SELECT CASE WHEN COL_LENGTH('dbo.' + @TableName, @ColumnName) IS NULL THEN 0 ELSE 1 END;", con, tx);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        private static void PrepareExternalRoutingRowForSave(DataRow row, bool hasRestorePriority) {
            if (!hasRestorePriority || !row.Table.Columns.Contains("RestorePriority") || !row.Table.Columns.Contains("Priority") || !row.Table.Columns.Contains("Active")) {
                return;
            }

            int currentPriority = row["Priority"] == DBNull.Value ? 1 : Convert.ToInt32(row["Priority"]);
            int currentRestorePriority = row["RestorePriority"] == DBNull.Value ? currentPriority : Convert.ToInt32(row["RestorePriority"]);
            bool currentActive = row["Active"] != DBNull.Value && Convert.ToBoolean(row["Active"]);

            if (row.RowState == DataRowState.Added) {
                row["RestorePriority"] = currentPriority;
                return;
            }

            if (row.RowState != DataRowState.Modified || !row.HasVersion(DataRowVersion.Original)) {
                if (currentRestorePriority <= 0) {
                    row["RestorePriority"] = currentPriority;
                }

                return;
            }

            bool originalActive = row["Active", DataRowVersion.Original] != DBNull.Value && Convert.ToBoolean(row["Active", DataRowVersion.Original]);
            int originalPriority = row["Priority", DataRowVersion.Original] == DBNull.Value ? currentPriority : Convert.ToInt32(row["Priority", DataRowVersion.Original]);
            bool priorityChanged = currentPriority != originalPriority;

            if (currentActive && !originalActive && !priorityChanged && currentRestorePriority > 0) {
                row["Priority"] = currentRestorePriority;
                row["RestorePriority"] = currentRestorePriority;
                return;
            }

            if (!currentActive && originalActive && !priorityChanged) {
                row["RestorePriority"] = originalPriority;
                return;
            }

            if (priorityChanged || currentActive || currentRestorePriority <= 0) {
                row["RestorePriority"] = Convert.ToInt32(row["Priority"]);
            }
        }

        private static void ReserveActiveExternalRoutingPriority(SqlConnection con, SqlTransaction tx, DataRow row, string internalColumnName, int routeId, bool hasRestorePriority) {
            if (!row.Table.Columns.Contains("Active") || !row.Table.Columns.Contains("Priority") || !row.Table.Columns.Contains(internalColumnName)) {
                return;
            }

            bool currentActive = row["Active"] != DBNull.Value && Convert.ToBoolean(row["Active"]);
            if (!currentActive) {
                return;
            }

            int currentInternalId = Convert.ToInt32(row[internalColumnName]);
            int currentPriority = row["Priority"] == DBNull.Value ? 1 : Convert.ToInt32(row["Priority"]);

            if (row.RowState == DataRowState.Added || row.RowState != DataRowState.Modified || !row.HasVersion(DataRowVersion.Original)) {
                ShiftActiveExternalRoutingPrioritiesUp(con, tx, currentInternalId, currentPriority, int.MaxValue, routeId, hasRestorePriority);
                return;
            }

            bool originalActive = row["Active", DataRowVersion.Original] != DBNull.Value && Convert.ToBoolean(row["Active", DataRowVersion.Original]);
            int originalPriority = row["Priority", DataRowVersion.Original] == DBNull.Value ? currentPriority : Convert.ToInt32(row["Priority", DataRowVersion.Original]);
            int originalInternalId = row[internalColumnName, DataRowVersion.Original] == DBNull.Value
                ? currentInternalId
                : Convert.ToInt32(row[internalColumnName, DataRowVersion.Original]);

            if (!originalActive || originalInternalId != currentInternalId) {
                ShiftActiveExternalRoutingPrioritiesUp(con, tx, currentInternalId, currentPriority, int.MaxValue, routeId, hasRestorePriority);
                return;
            }

            if (currentPriority == originalPriority) {
                return;
            }

            if (currentPriority < originalPriority) {
                ShiftActiveExternalRoutingPrioritiesUp(con, tx, currentInternalId, currentPriority, originalPriority - 1, routeId, hasRestorePriority);
            } else {
                ShiftActiveExternalRoutingPrioritiesDown(con, tx, currentInternalId, originalPriority + 1, currentPriority, routeId, hasRestorePriority);
            }
        }

        private static void ShiftActiveExternalRoutingPrioritiesUp(SqlConnection con, SqlTransaction tx, int internalNumberId, int fromPriority, int toPriority, int excludeRouteId, bool hasRestorePriority) {
            int from = fromPriority <= 0 ? 1 : fromPriority;
            string upperBound = toPriority == int.MaxValue ? string.Empty : " AND Priority <= @ToPriority";
            string restoreSet = hasRestorePriority ? ", RestorePriority = Priority + 1" : string.Empty;
            using SqlCommand cmd = new SqlCommand($@"
UPDATE dbo.TK_ExternalNumberRouting
SET Priority = Priority + 1{restoreSet},
    UpdatedAt = SYSDATETIME()
WHERE InternalNumberID = @InternalNumberID
  AND Active = 1
  AND ID <> @ExcludeRouteID
  AND Priority >= @FromPriority{upperBound};", con, tx);
            cmd.Parameters.AddWithValue("@InternalNumberID", internalNumberId);
            cmd.Parameters.AddWithValue("@ExcludeRouteID", excludeRouteId);
            cmd.Parameters.AddWithValue("@FromPriority", from);
            if (toPriority != int.MaxValue) {
                cmd.Parameters.AddWithValue("@ToPriority", toPriority);
            }

            cmd.ExecuteNonQuery();
        }

        private static void ShiftActiveExternalRoutingPrioritiesDown(SqlConnection con, SqlTransaction tx, int internalNumberId, int fromPriority, int toPriority, int excludeRouteId, bool hasRestorePriority) {
            int from = fromPriority <= 0 ? 1 : fromPriority;
            int to = toPriority <= 0 ? from : toPriority;
            string restoreSet = hasRestorePriority ? ", RestorePriority = CASE WHEN Priority - 1 <= 0 THEN 1 ELSE Priority - 1 END" : string.Empty;
            using SqlCommand cmd = new SqlCommand($@"
UPDATE dbo.TK_ExternalNumberRouting
SET Priority = CASE WHEN Priority - 1 <= 0 THEN 1 ELSE Priority - 1 END{restoreSet},
    UpdatedAt = SYSDATETIME()
WHERE InternalNumberID = @InternalNumberID
  AND Active = 1
  AND ID <> @ExcludeRouteID
  AND Priority >= @FromPriority
  AND Priority <= @ToPriority;", con, tx);
            cmd.Parameters.AddWithValue("@InternalNumberID", internalNumberId);
            cmd.Parameters.AddWithValue("@ExcludeRouteID", excludeRouteId);
            cmd.Parameters.AddWithValue("@FromPriority", from);
            cmd.Parameters.AddWithValue("@ToPriority", to);
            cmd.ExecuteNonQuery();
        }

        private static void AddAffectedInternalNumberId(DataRow row, string internalColumnName, List<int> affectedInternalIds, DataRowVersion version = DataRowVersion.Current) {
            if (!row.Table.Columns.Contains(internalColumnName)) {
                return;
            }

            object value;
            try {
                value = row[internalColumnName, version];
            } catch {
                return;
            }

            if (value != DBNull.Value && int.TryParse(Convert.ToString(value), out int id) && id > 0) {
                affectedInternalIds.Add(id);
            }
        }

        private static int GetNextFreeRoutingPriority(SqlConnection con, SqlTransaction tx, int internalNumberId, bool active, int requestedPriority, int excludeRouteId) {
            HashSet<int> used = new HashSet<int>();
            using SqlCommand cmd = new SqlCommand(@"
SELECT Priority
FROM dbo.TK_ExternalNumberRouting
WHERE InternalNumberID = @InternalNumberID
  AND Active = @Active
  AND ID <> @ExcludeRouteID;", con, tx);
            cmd.Parameters.AddWithValue("@InternalNumberID", internalNumberId);
            cmd.Parameters.AddWithValue("@Active", active);
            cmd.Parameters.AddWithValue("@ExcludeRouteID", excludeRouteId);

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read()) {
                if (reader["Priority"] != DBNull.Value) {
                    used.Add(Convert.ToInt32(reader["Priority"]));
                }
            }

            int priority = requestedPriority <= 0 ? 1 : requestedPriority;
            while (used.Contains(priority)) {
                priority++;
            }

            return priority;
        }

        private static void UpdateExternalRoutingActivationState(SqlConnection con, SqlTransaction tx, int routeId, bool active, int priority, int? restorePriority) {
            string restoreSet = restorePriority.HasValue ? ", RestorePriority = @RestorePriority" : string.Empty;
            using SqlCommand cmd = new SqlCommand($@"
UPDATE dbo.TK_ExternalNumberRouting
SET Active = @Active,
    Priority = @Priority{restoreSet},
    UpdatedAt = SYSDATETIME()
WHERE ID = @ID;", con, tx);
            cmd.Parameters.AddWithValue("@ID", routeId);
            cmd.Parameters.AddWithValue("@Active", active);
            cmd.Parameters.AddWithValue("@Priority", priority <= 0 ? 1 : priority);
            if (restorePriority.HasValue) {
                cmd.Parameters.AddWithValue("@RestorePriority", restorePriority.Value <= 0 ? 1 : restorePriority.Value);
            }

            cmd.ExecuteNonQuery();
        }

        private static void NormalizeExternalRoutingPriorities(SqlConnection con, SqlTransaction tx, IReadOnlyCollection<int> internalNumberIds) {
            if (internalNumberIds.Count == 0) {
                return;
            }

            bool hasRestorePriority = SqlColumnExists(con, tx, "TK_ExternalNumberRouting", "RestorePriority");
            string restoreSelect = hasRestorePriority ? ", RestorePriority" : string.Empty;
            string parameterList = string.Join(",", internalNumberIds.Select((_, index) => "@ID" + index));

            using SqlCommand selectCmd = new SqlCommand($@"
SELECT ID, InternalNumberID, Priority, Active{restoreSelect}
FROM dbo.TK_ExternalNumberRouting
WHERE InternalNumberID IN ({parameterList})
ORDER BY InternalNumberID ASC, Active DESC, Priority ASC, ID ASC;", con, tx);

            int parameterIndex = 0;
            foreach (int internalNumberId in internalNumberIds) {
                selectCmd.Parameters.AddWithValue("@ID" + parameterIndex, internalNumberId);
                parameterIndex++;
            }

            DataTable table = new DataTable();
            using (SqlDataAdapter adapter = new SqlDataAdapter(selectCmd)) {
                adapter.Fill(table);
            }

            List<ExternalRoutingPriorityUpdate> updates = new List<ExternalRoutingPriorityUpdate>();

            foreach (IGrouping<int, DataRow> group in table.Rows.Cast<DataRow>().GroupBy(r => Convert.ToInt32(r["InternalNumberID"]))) {
                int nextActivePriority = 1;
                foreach (DataRow row in group
                    .Where(r => Convert.ToBoolean(r["Active"]))
                    .OrderBy(r => Convert.ToInt32(r["Priority"]))
                    .ThenBy(r => Convert.ToInt32(r["ID"]))) {
                    int id = Convert.ToInt32(row["ID"]);
                    int oldPriority = Convert.ToInt32(row["Priority"]);
                    int newPriority = nextActivePriority++;
                    int? restorePriority = hasRestorePriority ? newPriority : null;
                    if (oldPriority != newPriority || (hasRestorePriority && Convert.ToInt32(row["RestorePriority"]) != newPriority)) {
                        updates.Add(new ExternalRoutingPriorityUpdate(id, newPriority, restorePriority));
                    }
                }

                HashSet<int> inactiveUsed = new HashSet<int>();
                foreach (DataRow row in group
                    .Where(r => !Convert.ToBoolean(r["Active"]))
                    .OrderBy(r => Convert.ToInt32(r["Priority"]))
                    .ThenBy(r => Convert.ToInt32(r["ID"]))) {
                    int id = Convert.ToInt32(row["ID"]);
                    int oldPriority = Convert.ToInt32(row["Priority"]);
                    int newPriority = oldPriority <= 0 ? 1 : oldPriority;
                    while (inactiveUsed.Contains(newPriority)) {
                        newPriority++;
                    }

                    inactiveUsed.Add(newPriority);
                    int? restorePriority = null;
                    if (hasRestorePriority) {
                        int oldRestore = row["RestorePriority"] == DBNull.Value ? newPriority : Convert.ToInt32(row["RestorePriority"]);
                        restorePriority = oldRestore <= 0 ? newPriority : oldRestore;
                    }

                    if (oldPriority != newPriority || (hasRestorePriority && row["RestorePriority"] == DBNull.Value)) {
                        updates.Add(new ExternalRoutingPriorityUpdate(id, newPriority, restorePriority));
                    }
                }
            }

            foreach (ExternalRoutingPriorityUpdate update in updates) {
                UpdateExternalRoutingActivationState(con, tx, update.RouteId, active: GetRouteActiveState(con, tx, update.RouteId), update.Priority, update.RestorePriority);
            }
        }

        private static bool GetRouteActiveState(SqlConnection con, SqlTransaction tx, int routeId) {
            using SqlCommand cmd = new SqlCommand("SELECT Active FROM dbo.TK_ExternalNumberRouting WHERE ID = @ID;", con, tx);
            cmd.Parameters.AddWithValue("@ID", routeId);
            object? value = cmd.ExecuteScalar();
            return value != null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        private sealed class ExternalRoutingRouteRow {
            public ExternalRoutingRouteRow(int routeId, int internalNumberId, int priority, bool active, int restorePriority) {
                RouteId = routeId;
                InternalNumberId = internalNumberId;
                Priority = priority;
                Active = active;
                RestorePriority = restorePriority;
            }

            public int RouteId { get; }

            public int InternalNumberId { get; }

            public int Priority { get; }

            public bool Active { get; }

            public int RestorePriority { get; }
        }

        private sealed class ExternalRoutingPriorityUpdate {
            public ExternalRoutingPriorityUpdate(int routeId, int priority, int? restorePriority) {
                RouteId = routeId;
                Priority = priority;
                RestorePriority = restorePriority;
            }

            public int RouteId { get; }

            public int Priority { get; }

            public int? RestorePriority { get; }
        }

        private void DeleteExternalNumberRoutingRow(SqlConnection con, SqlTransaction tx, DataRow row) {
            int id = Convert.ToInt32(row["ID", DataRowVersion.Original]);
            using SqlCommand cmd = new SqlCommand("DELETE FROM dbo.TK_ExternalNumberRouting WHERE ID = @ID;", con, tx);
            cmd.Parameters.AddWithValue("@ID", id);
            cmd.ExecuteNonQuery();
        }

        private static void AddExternalNumberRoutingParameters(SqlCommand cmd, DataRow row, string internalColumnName, bool hasInterneNrColumn, bool hasExterneNrColumn) {
            cmd.Parameters.AddWithValue("@ExternalNumberID", Convert.ToInt32(row["ExternalNumberID"]));
            cmd.Parameters.AddWithValue("@TargetType", NormalizeRoutingTargetType(Convert.ToString(row["TargetType"]) ?? "SIPUser"));
            cmd.Parameters.AddWithValue("@Priority", Convert.ToInt32(row["Priority"]));
            cmd.Parameters.AddWithValue("@Active", row["Active"] == DBNull.Value ? false : Convert.ToBoolean(row["Active"]));
            cmd.Parameters.AddWithValue("@" + internalColumnName, Convert.ToInt32(row[internalColumnName]));

            if (hasInterneNrColumn) {
                cmd.Parameters.AddWithValue("@InterneNr", Convert.ToString(row["InterneNr"]) ?? string.Empty);
            }

            if (hasExterneNrColumn) {
                cmd.Parameters.AddWithValue("@ExterneNr", Convert.ToString(row["ExterneNr"]) ?? string.Empty);
            }

            if (row.Table.Columns.Contains("RestorePriority")) {
                int restorePriority = row["RestorePriority"] == DBNull.Value ? Convert.ToInt32(row["Priority"]) : Convert.ToInt32(row["RestorePriority"]);
                cmd.Parameters.AddWithValue("@RestorePriority", restorePriority);
            }
        }

        private static string NormalizeRoutingTargetType(string value) {
            string trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(trimmed, "FAXUser", StringComparison.OrdinalIgnoreCase)) {
                return "FAXUser";
            }

            return "SIPUser";
        }

        private static void InsertVoipUser(SqlConnection con, SqlTransaction tx, DataRow row) {
            ValidateVoipUserRow(row, isNew: true);

            using SqlCommand cmd = new SqlCommand(@"
INSERT INTO dbo.TK_SIP_User
(
    Username,
    Telefonnummer,
    [Password],
    DisplayName,
    Email,
    [Description],
    UserType,
    Active,
    CanDialInternal,
    CanDialExternal,
    CanReceiveInternal,
    CanReceiveExternal,
    SortOrder,
    CreatedAt,
    UpdatedAt
)
VALUES
(
    @Username,
    @Telefonnummer,
    @Password,
    @DisplayName,
    @Email,
    @Description,
    @UserType,
    @Active,
    @CanDialInternal,
    @CanDialExternal,
    @CanReceiveInternal,
    @CanReceiveExternal,
    @SortOrder,
    @CreatedAt,
    @UpdatedAt
);", con, tx);

            AddVoipUserParameters(cmd, row, isNew: true);
            cmd.ExecuteNonQuery();
        }

        private static void UpdateVoipUser(SqlConnection con, SqlTransaction tx, DataRow row) {
            ValidateVoipUserRow(row, isNew: false);

            using SqlCommand cmd = new SqlCommand(@"
UPDATE dbo.TK_SIP_User
SET
    Username = @Username,
    Telefonnummer = @Telefonnummer,
    [Password] = @Password,
    DisplayName = @DisplayName,
    Email = @Email,
    [Description] = @Description,
    UserType = @UserType,
    Active = @Active,
    CanDialInternal = @CanDialInternal,
    CanDialExternal = @CanDialExternal,
    CanReceiveInternal = @CanReceiveInternal,
    CanReceiveExternal = @CanReceiveExternal,
    SortOrder = @SortOrder,
    UpdatedAt = @UpdatedAt
WHERE ID = @ID;", con, tx);

            AddVoipUserParameters(cmd, row, isNew: false);
            cmd.Parameters.AddWithValue("@ID", Convert.ToInt32(row["ID"]));
            cmd.ExecuteNonQuery();
        }

        private static void AddVoipUserParameters(SqlCommand cmd, DataRow row, bool isNew) {
            DateTime now = DateTime.Now;
            cmd.Parameters.AddWithValue("@Username", RequiredText(row, "Username"));
            cmd.Parameters.AddWithValue("@Telefonnummer", RequiredText(row, "Telefonnummer"));
            cmd.Parameters.AddWithValue("@Password", SipCryptor.EncryptFixed(RequiredText(row, "Password")));
            cmd.Parameters.AddWithValue("@DisplayName", OptionalText(row, "DisplayName"));
            cmd.Parameters.AddWithValue("@Email", OptionalText(row, "Email"));
            cmd.Parameters.AddWithValue("@Description", OptionalText(row, "Description"));
            cmd.Parameters.AddWithValue("@UserType", DefaultText(row, "UserType", "User"));
            cmd.Parameters.AddWithValue("@Active", DefaultBool(row, "Active", true));
            cmd.Parameters.AddWithValue("@CanDialInternal", DefaultBool(row, "CanDialInternal", true));
            cmd.Parameters.AddWithValue("@CanDialExternal", DefaultBool(row, "CanDialExternal", true));
            cmd.Parameters.AddWithValue("@CanReceiveInternal", DefaultBool(row, "CanReceiveInternal", true));
            cmd.Parameters.AddWithValue("@CanReceiveExternal", DefaultBool(row, "CanReceiveExternal", true));
            cmd.Parameters.AddWithValue("@SortOrder", DefaultInt(row, "SortOrder", 0));

            if (isNew) {
                cmd.Parameters.AddWithValue("@CreatedAt", GetDateTimeOrDefault(row, "CreatedAt", now));
            }

            cmd.Parameters.AddWithValue("@UpdatedAt", now);
        }

        private static void ValidateVoipUserRow(DataRow row, bool isNew) {
            _ = RequiredText(row, "Username");
            _ = RequiredText(row, "Telefonnummer");
            _ = RequiredText(row, "Password");

            if (!isNew && (row["ID"] == DBNull.Value || Convert.ToInt32(row["ID"]) <= 0)) {
                throw new InvalidOperationException("Ein bestehender Benutzer hat keine gÃƒÂ¼ltige ID.");
            }
        }

        private static string RequiredText(DataRow row, string columnName) {
            string value = row[columnName]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"Feld '{columnName}' darf nicht leer sein.");
            }
            return value;
        }

        private static string OptionalText(DataRow row, string columnName) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? row[columnName]?.ToString()?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string DefaultText(DataRow row, string columnName, string defaultValue) {
            string value = OptionalText(row, columnName);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static bool DefaultBool(DataRow row, string columnName, bool defaultValue) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToBoolean(row[columnName])
                : defaultValue;
        }

        private static int DefaultInt(DataRow row, string columnName, int defaultValue) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToInt32(row[columnName])
                : defaultValue;
        }

        private static DateTime GetDateTimeOrDefault(DataRow row, string columnName, DateTime defaultValue) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToDateTime(row[columnName])
                : defaultValue;
        }

        private static object DbOptionalString(string? value) {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
        }


        /// <summary>
        /// Dynamisches INSERT.
        /// ÃƒÅ“bergibt nur Tabellenname + Felder/Werte.
        /// CreatedAt und UpdatedAt werden automatisch ergÃƒÂ¤nzt, wenn sie nicht mitgegeben wurden.
        /// Gibt die neue ID zurÃƒÂ¼ck, wenn OUTPUT INSERTED.ID mÃƒÂ¶glich ist
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <param name="fields">The fields<see cref="SqlFieldValue[]"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int InsertDynamic(string tableName, params SqlFieldValue[] fields) {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tabellenname ist leer.", nameof(tableName));

            if (fields == null || fields.Length == 0)
                throw new ArgumentException("Es wurden keine Felder ÃƒÂ¼bergeben.", nameof(fields));

            tableName = NormalizeSqlIdentifier(tableName);

            List<string> columns = new List<string>();
            List<string> parameters = new List<string>();

            SqlConnection con = TKSQL_ConnectionClass.GetConnection();

            using SqlCommand cmd = new SqlCommand();
            cmd.Connection = con;

            for (int i = 0; i < fields.Length; i++) {
                if (string.IsNullOrWhiteSpace(fields[i].Name))
                    throw new ArgumentException($"Feldname an Position {i} ist leer.");

                string columnName = NormalizeSqlIdentifier(fields[i].Name);
                string parameterName = $"@p{i}";

                columns.Add($"[{columnName}]");
                parameters.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, ToDbValue(fields[i].Value));
            }

            if (!ContainsField(fields, "CreatedAt")) {
                columns.Add("[CreatedAt]");
                parameters.Add("GETDATE()");
            }

            if (!ContainsField(fields, "UpdatedAt")) {
                columns.Add("[UpdatedAt]");
                parameters.Add("GETDATE()");
            }

            cmd.CommandText = $@"
INSERT INTO [dbo].[{tableName}]
(
    {string.Join(", ", columns)}
)
OUTPUT INSERTED.ID
VALUES
(
    {string.Join(", ", parameters)}
);";

            object? result = cmd.ExecuteScalar();
            int newId = Convert.ToInt32(result);

            _logger?.Info($"InsertDynamic OK | Tabelle={tableName} | ID={newId}");
            return newId;
        }

        /// <summary>
        /// Dynamisches UPDATE anhand der ID.
        /// UpdatedAt wird automatisch gesetzt, wenn es nicht mitgegeben wurde.
        /// Gibt die Anzahl der betroffenen Zeilen zurÃƒÂ¼ck
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <param name="id">The id<see cref="int"/></param>
        /// <param name="fields">The fields<see cref="SqlFieldValue[]"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpdateDynamic(string tableName, int id, params SqlFieldValue[] fields) {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tabellenname ist leer.", nameof(tableName));

            if (id <= 0)
                throw new ArgumentException("ID muss grÃƒÂ¶ÃƒÅ¸er 0 sein.", nameof(id));

            if (fields == null || fields.Length == 0)
                throw new ArgumentException("Es wurden keine Felder ÃƒÂ¼bergeben.", nameof(fields));

            tableName = NormalizeSqlIdentifier(tableName);

            List<string> setParts = new List<string>();

            SqlConnection con = TKSQL_ConnectionClass.GetConnection();

            using SqlCommand cmd = new SqlCommand();
            cmd.Connection = con;

            int paramIndex = 0;

            for (int i = 0; i < fields.Length; i++) {
                if (string.IsNullOrWhiteSpace(fields[i].Name))
                    throw new ArgumentException($"Feldname an Position {i} ist leer.");

                string columnName = NormalizeSqlIdentifier(fields[i].Name);

                if (columnName.Equals("ID", StringComparison.OrdinalIgnoreCase))
                    continue;

                string parameterName = $"@p{paramIndex}";
                setParts.Add($"[{columnName}] = {parameterName}");
                cmd.Parameters.AddWithValue(parameterName, ToDbValue(fields[i].Value));
                paramIndex++;
            }

            if (!ContainsField(fields, "UpdatedAt")) {
                setParts.Add("[UpdatedAt] = GETDATE()");
            }

            cmd.Parameters.AddWithValue("@ID", id);

            cmd.CommandText = $@"
UPDATE [dbo].[{tableName}]
SET
    {string.Join(", ", setParts)}
WHERE [ID] = @ID;";

            int rows = cmd.ExecuteNonQuery();

            _logger?.Info($"UpdateDynamic OK | Tabelle={tableName} | ID={id} | Rows={rows}");
            return rows;
        }

        /// <summary>
        /// Dynamisches UPSERT.
        /// Sucht ÃƒÂ¼ber eine SchlÃƒÂ¼sselspalte.
        /// Existiert der Datensatz -> UpdateDynamic
        /// Existiert er nicht -> InsertDynamic
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <param name="keyColumn">The keyColumn<see cref="string"/></param>
        /// <param name="keyValue">The keyValue<see cref="object?"/></param>
        /// <param name="fields">The fields<see cref="SqlFieldValue[]"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpsertDynamic(string tableName, string keyColumn, object? keyValue, params SqlFieldValue[] fields) {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tabellenname ist leer.", nameof(tableName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentException("SchlÃƒÂ¼sselspalte ist leer.", nameof(keyColumn));

            tableName = NormalizeSqlIdentifier(tableName);
            keyColumn = NormalizeSqlIdentifier(keyColumn);

            SqlConnection con = TKSQL_ConnectionClass.GetConnection();

            using SqlCommand findCmd = new SqlCommand();
            findCmd.Connection = con;

            if (keyValue == null || keyValue == DBNull.Value) {
                findCmd.CommandText = $@"
SELECT TOP 1 [ID]
FROM [dbo].[{tableName}]
WHERE [{keyColumn}] IS NULL;";
            } else {
                findCmd.CommandText = $@"
SELECT TOP 1 [ID]
FROM [dbo].[{tableName}]
WHERE [{keyColumn}] = @KeyValue;";
                findCmd.Parameters.AddWithValue("@KeyValue", ToDbValue(keyValue));
            }

            object? existing = findCmd.ExecuteScalar();

            if (existing != null && existing != DBNull.Value) {
                int id = Convert.ToInt32(existing);
                UpdateDynamic(tableName, id, fields);
                return id;
            }

            return InsertDynamic(tableName, fields);
        }

        /// <summary>
        /// Dynamisches UPSERT mit mehreren SchlÃƒÂ¼sselspalten.
        /// keys bestimmt die WHERE-Bedingung.
        /// fields sind die zu schreibenden Werte
        /// </summary>
        /// <param name="tableName">The tableName<see cref="string"/></param>
        /// <param name="keys">The keys<see cref="SqlFieldValue[]"/></param>
        /// <param name="fields">The fields<see cref="SqlFieldValue[]"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int UpsertDynamic(string tableName, SqlFieldValue[] keys, params SqlFieldValue[] fields) {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tabellenname ist leer.", nameof(tableName));

            if (keys == null || keys.Length == 0)
                throw new ArgumentException("Es wurden keine SchlÃƒÂ¼ssel ÃƒÂ¼bergeben.", nameof(keys));

            if (fields == null || fields.Length == 0)
                throw new ArgumentException("Es wurden keine Felder ÃƒÂ¼bergeben.", nameof(fields));

            tableName = NormalizeSqlIdentifier(tableName);

            SqlConnection con = TKSQL_ConnectionClass.GetConnection();

            using SqlCommand findCmd = new SqlCommand();
            findCmd.Connection = con;

            List<string> whereParts = new List<string>();

            for (int i = 0; i < keys.Length; i++) {
                if (string.IsNullOrWhiteSpace(keys[i].Name))
                    throw new ArgumentException($"SchlÃƒÂ¼sselname an Position {i} ist leer.");

                string keyColumn = NormalizeSqlIdentifier(keys[i].Name);

                if (keys[i].Value == null || keys[i].Value == DBNull.Value) {
                    whereParts.Add($"[{keyColumn}] IS NULL");
                } else {
                    string parameterName = $"@k{i}";
                    whereParts.Add($"[{keyColumn}] = {parameterName}");
                    findCmd.Parameters.AddWithValue(parameterName, ToDbValue(keys[i].Value));
                }
            }

            findCmd.CommandText = $@"
SELECT TOP 1 [ID]
FROM [dbo].[{tableName}]
WHERE {string.Join(" AND ", whereParts)};";

            object? existing = findCmd.ExecuteScalar();

            if (existing != null && existing != DBNull.Value) {
                int id = Convert.ToInt32(existing);
                UpdateDynamic(tableName, id, MergeFields(keys, fields));
                return id;
            }

            return InsertDynamic(tableName, MergeFields(keys, fields));
        }

        /// <summary>
        /// The MergeFields
        /// </summary>
        /// <param name="keys">The keys<see cref="SqlFieldValue[]"/></param>
        /// <param name="fields">The fields<see cref="SqlFieldValue[]"/></param>
        /// <returns>The <see cref="SqlFieldValue[]"/></returns>
        private static SqlFieldValue[] MergeFields(SqlFieldValue[] keys, SqlFieldValue[] fields) {
            Dictionary<string, SqlFieldValue> merged = new Dictionary<string, SqlFieldValue>(StringComparer.OrdinalIgnoreCase);

            foreach (SqlFieldValue key in keys) {
                merged[key.Name] = key;
            }

            foreach (SqlFieldValue field in fields) {
                merged[field.Name] = field;
            }

            SqlFieldValue[] result = new SqlFieldValue[merged.Count];
            int i = 0;

            foreach (KeyValuePair<string, SqlFieldValue> item in merged) {
                result[i] = item.Value;
                i++;
            }

            return result;
        }

        /// <summary>
        /// The ContainsField
        /// </summary>
        /// <param name="fields">The fields<see cref="SqlFieldValue[]"/></param>
        /// <param name="fieldName">The fieldName<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool ContainsField(SqlFieldValue[] fields, string fieldName) {
            foreach (SqlFieldValue field in fields) {
                if (field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The ToDbValue
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="object"/></returns>
        private static object ToDbValue(object? value) {
            return value ?? DBNull.Value;
        }

        /// <summary>
        /// The NormalizeSqlIdentifier
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeSqlIdentifier(string name) {
            name = name.Trim();

            if (!Regex.IsMatch(name, @"^[A-Za-z0-9_]+$"))
                throw new ArgumentException($"UngÃƒÂ¼ltiger SQL-Bezeichner: {name}");

            return name;
        }

        /// <summary>
        /// The ExecuteList
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command<see cref="SqlCommand"/></param>
        /// <param name="mapper">The mapper<see cref="Func{DataRow, T}"/></param>
        /// <returns>The <see cref="List{T}"/></returns>
        private static List<T> ExecuteList<T>(SqlCommand command, Func<DataRow, T> mapper) {
            using SqlDataAdapter adapter = new SqlDataAdapter(command);
            DataTable table = new DataTable();
            adapter.Fill(table);

            List<T> result = new List<T>(table.Rows.Count);
            foreach (DataRow row in table.Rows) {
                result.Add(mapper(row));
            }

            return result;
        }

        /// <summary>
        /// The ExecuteRows
        /// </summary>
        /// <param name="command">The command<see cref="SqlCommand"/></param>
        /// <returns>The <see cref="List{DataRow}"/></returns>
        private static List<DataRow> ExecuteRows(SqlCommand command) {
            using SqlDataAdapter adapter = new SqlDataAdapter(command);
            DataTable table = new DataTable();
            adapter.Fill(table);

            List<DataRow> result = new List<DataRow>(table.Rows.Count);
            foreach (DataRow row in table.Rows) {
                result.Add(row);
            }

            return result;
        }

        /// <summary>
        /// The ExecuteSingleRow
        /// </summary>
        /// <param name="command">The command<see cref="SqlCommand"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private static DataRow? ExecuteSingleRow(SqlCommand command) {
            using SqlDataAdapter adapter = new SqlDataAdapter(command);
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table.Rows.Count > 0 ? table.Rows[0] : null;
        }
    }

    /// <summary>
    /// Feld-/Wert-Paar fuer zentrale SQL-Commandmethoden.
    /// </summary>
    public sealed class SqlFieldValue {
        /// <summary>
        /// Gets or sets the Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Value
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlFieldValue"/> class.
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="value">The value<see cref="object?"/></param>
        public SqlFieldValue(string name, object? value) {
            Name = name;
            Value = value;
        }
    }

}
