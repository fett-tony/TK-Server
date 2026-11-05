/*
 * TKBase_RuntimeCallEvents
 * Aufgabe:
 * - Meldet Änderungen der Runtime-Call-Sicht nach außen.
 * - Dient der Benachrichtigung von UI- oder Beobachter-Komponenten, insbesondere der LiveCall-Form.
 * - Signalisiert, dass sich die aktuelle Laufzeitsicht aktiver Gespräche geändert hat.
 *
 * Nicht Aufgabe:
 * - Keine eigene Runtime-Call-Verwaltung.
 * - Keine Routingentscheidung.
 * - Keine Session-Steuerung.
 * - Keine Call-Steuerung.
 * - Keine Media-Steuerung.
 *
 * Diese Datei ist die Änderungs- und Benachrichtigungsschicht der Runtime-Call-Sicht.
 */

namespace TKServer.BASE {
    using System;

    /// TKBase_RuntimeCallEvents
    /// Aufgabe:
    /// - Meldet Änderungen der Runtime-Call-Sicht nach außen.
    /// - Dient der Benachrichtigung von UI- oder Beobachter-Komponenten, insbesondere der LiveCall-Form.
    /// - Signalisiert, dass sich die aktuelle Laufzeitsicht aktiver Gespräche geändert hat.
    /// 
    /// Nicht Aufgabe:
    /// - Keine eigene Runtime-Call-Verwaltung.
    /// - Keine Routingentscheidung.
    /// - Keine Session-Steuerung.
    /// - Keine Call-Steuerung.
    /// - Keine Media-Steuerung.
    /// 
    /// Diese Datei ist die Änderungs- und Benachrichtigungsschicht der Runtime-Call-Sicht.

    /// <summary>
    /// Defines the <see cref="TKBase_RuntimeCallEvents" />
    /// </summary>
    internal static class TKBase_RuntimeCallEvents {
        /// <summary>
        /// Defines the Changed
        /// </summary>
        public static event Action? Changed;

        /// <summary>
        /// The RaiseChanged
        /// </summary>
        public static void RaiseChanged() {
            try {
                Changed?.Invoke();
            } catch {
            }
        }
    }
}
