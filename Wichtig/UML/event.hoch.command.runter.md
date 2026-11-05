```mermaid
flowchart TB

    M["TKBase_SessionManager
    Session-Zuordnung
    Leg-Mapping
    Routing zwischen Leg-A / Leg-B / Leg-D"]

    I["TKSipInt_Session
    SIP intern"]
    E["TKSipExt_Session
    SIP extern / Provider"]
    G["TKGSM_Session
    GSM / AT"]
    S["TKSVC_Session
    Service"]

    I -->|"Event nach oben
    Trying / Ringing / Answered / Ended / Failed / MediaReady"| M

    E -->|"Event nach oben
    Trying / Ringing / Answered / Ended / Failed / MediaReady"| M

    G -->|"Event nach oben
    Trying / Ringing / Answered / Ended / Failed / MediaReady"| M

    S -->|"Event nach oben
    Trying / Ringing / Answered / Ended / Failed / MediaReady"| M

    M -->|"Command nach unten
    NotifyPeerTrying
    NotifyPeerRinging
    NotifyPeerAnswered
    RequestPeerHangup
    NotifyPeerEnded
    NotifyPeerFailed"| I

    M -->|"Command nach unten
    NotifyPeerTrying
    NotifyPeerRinging
    NotifyPeerAnswered
    RequestPeerHangup
    NotifyPeerEnded
    NotifyPeerFailed"| E

    M -->|"Command nach unten
    NotifyPeerTrying
    NotifyPeerRinging
    NotifyPeerAnswered
    RequestPeerHangup
    NotifyPeerEnded
    NotifyPeerFailed"| G

    M -->|"Command nach unten
    NotifyPeerTrying
    NotifyPeerRinging
    NotifyPeerAnswered
    RequestPeerHangup
    NotifyPeerEnded
    NotifyPeerFailed"| S


```