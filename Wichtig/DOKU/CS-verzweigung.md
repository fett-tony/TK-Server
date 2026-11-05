TK-Server/
├── Einstieg
│   └── Program.cs
│
├── Base / zentrale Server-Orchestrierung
│   └── TKBase_Server.cs
│   	├── TKBase_SessionManager.cs
│ 		│	├── TKSipInt_Session.cs
│   	│	│	└── TKSipInt_CallController.cs
│   	│	│		├── TKSipInt_Command.cs
│   	│	│		├── TKSipInt_Event.cs
│   	│	│		└── TKSipInt_Forking.cs
│   	│	├── TKSipExt_Session.cs
│   	│	│	└── TKSipExt_CallController.cs
│   	│	│		├── TKSipExt_Command.cs
│   	│	│		├── TKSipExt_Event.cs
│   	│	│		└── TKSipExt_Forking.cs
│   	│	├── TKGSM_Session.cs
│   	│	│	└── TKGSM_CallController.cs
│   	│	│		├── TKGSM_Command.cs
│   	│	│		├── TKGSM_Event.cs
│   	│	│		└── TKGSM_Forking.cs
│   	│	└── TKSVC_Session.cs
│   	│		└── TKSVC_CallController.cs
│   	│			├── TKSVC_Commands.cs
│   	│			├── TKSVC_Events.cs
│   	│			└── TKSVC_Forking.cs
│   	├── TKBase_RoutingManager.cs
│   	│	├── TKSipInt_Routing.cs
│   	│	├── TKSipExt_Routing.cs
│   	│	├── TKGSM_Routing.cs
│   	│	└── TKSVC_Routing.cs
│   	├── TKBase_RegistrationManager.cs
│   	│	├── TKSipInt_Registration.cs
│   	│	│	└── TKSipInt_Authentication.cs
│   	│	├── TKSipExt_Registration.cs
│   	│	│	└── TKSipExt_TransportProfile.cs
│   	│	└── TKGSM_Registration.cs
│   	│		├── TKGSM_DeviceRegister.cs
│   	│		└── TKGSM_DeviceMapping.cs
│   	├── TKBase_MediaManager.cs
│   	│	├── TKSipInt_Media.cs
│   	│	├── TKSipExt_Media.cs
│   	│	├── TKGSM_Media.cs
│   	│	└── TKSVC_Media.cs
│   	├── TKBase_RuntimeCallManager.cs
│   	├── TKBase_RuntimeCallEvents.cs
│   	├── TKBase_AudioCodecConfig.cs
│   	├── TKBase_SipGateway.cs
│   	├── TKBase_GsmGateway.cs
│   	└── TKBase_LogManager.cs
│
├── SIP Intern
│
├── SIP Extern / Provider
│
├── GSM
│
├── SVC / Service-Legs
│   ├── TKSVC_ZeitAnsage.cs
│   ├── TKSVC_ServiceRegistry.cs
│   └── TKSVC_ServiceTypes.cs
│
├── SQL / Persistenz
│   ├── TKSQL_ConnectionClass.cs
│   ├── TKSQL_Commands.cs
│   └── TKSQL_DB.Designer.cs
│
└── UI / Forms + Designer
    ├── 11 Formular-Dateien
    ├── 11 Designer-Dateien
    └── TK_GuiCommand.cs