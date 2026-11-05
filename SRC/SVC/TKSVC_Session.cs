/*
 * TKSVC_Session
 * Aufgabe:
 * - Hält die Service-Definition und die Service-Leg-D-Sicht.
 * - Verwaltet keine globale Sessionliste und keine Transportlogik.
 */

namespace TKServer.SVC {
    using System;
    using System.Data;


    /// TKSVC_Session
    /// Aufgabe:
    /// - Hält die Service-Definition und die Service-Leg-D-Sicht.
    /// - Verwaltet keine globale Sessionliste und keine Transportlogik.

    /// <summary>
    /// Defines the <see cref="TKSVC_Session" />
    /// </summary>
    public sealed class TKSVC_Session {
        /// <summary>
        /// Gets or sets the ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the ServiceGuid
        /// </summary>
        public Guid? ServiceGuid { get; set; }

        /// <summary>
        /// Gets or sets the InternalNumber
        /// </summary>
        public string InternalNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the DisplayName
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ServiceCode
        /// </summary>
        public string ServiceCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ServiceType
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the PromptFile
        /// </summary>
        public string PromptFile { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RecordTemplate
        /// </summary>
        public string RecordTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JsonConfig
        /// </summary>
        public string JsonConfig { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether AutoAnswer
        /// </summary>
        public bool AutoAnswer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether KeepSessionUntilPeerEnded
        /// </summary>
        public bool KeepSessionUntilPeerEnded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Active
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the MenuRootNumber
        /// </summary>
        public string MenuRootNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MenuOption
        /// </summary>
        public string MenuOption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SortOrder
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets the CreatedAt
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the UpdatedAt
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets a value indicating whether HasPromptFile
        /// </summary>
        public bool HasPromptFile => !string.IsNullOrWhiteSpace(PromptFile);

        /// <summary>
        /// Gets a value indicating whether HasRecordTemplate
        /// </summary>
        public bool HasRecordTemplate => !string.IsNullOrWhiteSpace(RecordTemplate);

        /// <summary>
        /// Gets a value indicating whether HasJsonConfig
        /// </summary>
        public bool HasJsonConfig => !string.IsNullOrWhiteSpace(JsonConfig);

        /// <summary>
        /// Gets a value indicating whether IsValid
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(InternalNumber) && !string.IsNullOrWhiteSpace(ServiceType);

        /// <summary>
        /// The MatchesServiceType
        /// </summary>
        /// <param name="serviceType">The serviceType<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool MatchesServiceType(string serviceType) {
            return string.Equals(ServiceType, TKSVC_ServiceTypes.Normalize(serviceType), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The MatchesServiceCode
        /// </summary>
        /// <param name="serviceCode">The serviceCode<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool MatchesServiceCode(string serviceCode) {
            return string.Equals(ServiceCode, SIP_ServiceCodes.Normalize(serviceCode), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The FromDataRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="TKSVC_Session"/></returns>
        public static TKSVC_Session FromDataRow(DataRow row) {
            return new TKSVC_Session {
                ID = ReadInt(row, "ID"),
                ServiceGuid = ReadGuid(row, "GUID"),
                InternalNumber = ReadString(row, "InternalNumber"),
                DisplayName = ReadString(row, "DisplayName"),
                Description = ReadString(row, "Description"),
                ServiceCode = SIP_ServiceCodes.Normalize(ReadString(row, "ServiceCode")),
                ServiceType = TKSVC_ServiceTypes.Normalize(ReadString(row, "ServiceType")),
                PromptFile = ReadString(row, "PromptFile"),
                RecordTemplate = ReadString(row, "RecordTemplate"),
                JsonConfig = ReadString(row, "JsonConfig"),
                AutoAnswer = ReadBool(row, "AutoAnswer"),
                KeepSessionUntilPeerEnded = ReadBool(row, "KeepSessionUntilPeerEnded"),
                Active = ReadBool(row, "Active"),
                MenuRootNumber = ReadString(row, "MenuRootNumber"),
                MenuOption = ReadString(row, "MenuOption"),
                SortOrder = ReadInt(row, "SortOrder"),
                CreatedAt = ReadDateTime(row, "CreatedAt"),
                UpdatedAt = ReadDateTime(row, "UpdatedAt")
            };
        }

        /// <summary>
        /// The ReadString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ReadString(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return string.Empty;
            return Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// The ReadInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReadInt(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return 0;
            return Convert.ToInt32(row[columnName]);
        }

        /// <summary>
        /// The ReadBool
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool ReadBool(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return false;
            object value = row[columnName];
            if (value is bool b) return b;
            if (value is byte by) return by != 0;
            if (value is short s) return s != 0;
            if (value is int i) return i != 0;
            string text = Convert.ToString(value)?.Trim() ?? string.Empty;
            if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase)) return false;
            return bool.TryParse(text, out bool parsed) && parsed;
        }

        /// <summary>
        /// The ReadGuid
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="Guid?"/></returns>
        private static Guid? ReadGuid(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            object value = row[columnName];
            if (value is Guid g) return g;
            string text = Convert.ToString(value)?.Trim() ?? string.Empty;
            return Guid.TryParse(text, out Guid parsed) ? parsed : null;
        }

        /// <summary>
        /// The ReadDateTime
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="DateTime?"/></returns>
        private static DateTime? ReadDateTime(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            object value = row[columnName];
            if (value is DateTime dt) return dt;
            string text = Convert.ToString(value)?.Trim() ?? string.Empty;
            return DateTime.TryParse(text, out DateTime parsed) ? parsed : null;
        }
    }
}
