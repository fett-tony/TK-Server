# TK-Server CS-Dateistruktur Tree V0001

Quelle: `2026.04.26_02.28_TK-Server.zip`

Enthalten: alle gefundenen `.cs`-Dateien inklusive Forms und Designer-Dateien.

Anzahl `.cs`-Dateien: 76

```text
TK-Server/
├── Einstieg
│   └── Program.cs
│
├── Base / zentrale Manager
│   ├── TKBase_AudioCodecConfig.cs
│   ├── TKBase_GsmGateway.cs
│   ├── TKBase_LogManager.cs
│   ├── TKBase_MediaManager.cs
│   ├── TKBase_RegistrationManager.cs
│   ├── TKBase_RoutingManager.cs
│   ├── TKBase_RuntimeCallEvents.cs
│   ├── TKBase_RuntimeCallManager.cs
│   ├── TKBase_Server.cs
│   ├── TKBase_SessionManager.cs
│   └── TKBase_SipGateway.cs
│
├── GSM-Bereich
│   ├── TKGSM_CallController.cs
│   ├── TKGSM_Command.cs
│   ├── TKGSM_DeviceMapping.cs
│   ├── TKGSM_DeviceRegister.cs
│   ├── TKGSM_Event.cs
│   ├── TKGSM_Forking.cs
│   ├── TKGSM_Media.cs
│   ├── TKGSM_Registration.cs
│   ├── TKGSM_Routing.cs
│   └── TKGSM_Session.cs
│
├── SIP Extern / Provider
│   ├── TKSipExt_CallController.cs
│   ├── TKSipExt_Command.cs
│   ├── TKSipExt_Event.cs
│   ├── TKSipExt_Forking.cs
│   ├── TKSipExt_Media.cs
│   ├── TKSipExt_Registration.cs
│   ├── TKSipExt_Routing.cs
│   ├── TKSipExt_Session.cs
│   └── TKSipExt_TransportProfile.cs
│
├── SIP Intern
│   ├── TKSipInt_Authentication.cs
│   ├── TKSipInt_CallController.cs
│   ├── TKSipInt_Command.cs
│   ├── TKSipInt_Event.cs
│   ├── TKSipInt_Forking.cs
│   ├── TKSipInt_Media.cs
│   ├── TKSipInt_Registration.cs
│   ├── TKSipInt_Routing.cs
│   └── TKSipInt_Session.cs
│
├── SQL / Datenzugriff
│   ├── TKSQL_Commands.cs
│   ├── TKSQL_ConnectionClass.cs
│   └── TKSQL_DB.Designer.cs
│
├── Service-Legs / SVC
│   ├── TKSVC_CallController.cs
│   ├── TKSVC_Commands.cs
│   ├── TKSVC_Events.cs
│   ├── TKSVC_Forking.cs
│   ├── TKSVC_Media.cs
│   ├── TKSVC_Routing.cs
│   ├── TKSVC_ServiceRegistry.cs
│   ├── TKSVC_ServiceTypes.cs
│   ├── TKSVC_Session.cs
│   └── TKSVC_ZeitAnsage.cs
│
├── UI / Forms
│   ├── TK_CallLog.cs
│   ├── TK_CallLog.Designer.cs
│   ├── TK_EmergencyCallRule.cs
│   ├── TK_EmergencyCallRule.Designer.cs
│   ├── TK_GSMDevice.cs
│   ├── TK_GSMDevice.Designer.cs
│   ├── TK_LiveCallState.cs
│   ├── TK_LiveCallState.Designer.cs
│   ├── TK_NumberRouting.cs
│   ├── TK_NumberRouting.Designer.cs
│   ├── TK_ProviderAccounts.cs
│   ├── TK_ProviderAccounts.Designer.cs
│   ├── TK_Server.cs
│   ├── TK_Server.Designer.cs
│   ├── TK_ServerEinstellung.cs
│   ├── TK_ServerEinstellung.Designer.cs
│   ├── TK_ServiceLegs.cs
│   ├── TK_ServiceLegs.Designer.cs
│   ├── TK_Start.cs
│   ├── TK_Start.Designer.cs
│   ├── TK_VoIPUser.cs
│   └── TK_VoIPUser.Designer.cs
│
└── UI / Hilfslogik
    └── TK_GuiCommand.cs
```
