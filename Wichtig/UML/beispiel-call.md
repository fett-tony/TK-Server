```mermaid

flowchart LR

    A["TKSipInt_Session
    Leg-A
    1005"] -->|"Event nach oben
    1.Invite gestartet"| M["TKBase_SessionManager"]

    M -->|"Command nach unten
    1.StartOutboundCall für Leg-B"| G["TKGSM_Session
    Leg-B"]

    G -->|"ATD / CLCC=Trying
    Event nach oben: Trying"| M
    M -->|"Command nach unten
    NotifyPeerTrying"| A

    G -->|"CLCC=Ringing
    Event nach oben: Ringing"| M
    M -->|"Command nach unten
    NotifyPeerRinging"| A

    G -->|"VOICE CALL: BEGIN
    Event nach oben: Answered"| M
    M -->|"Command nach unten
    NotifyPeerAnswered"| A

    A -->|"BYE empfangen
    Event nach oben: HangupRequested"| M
    M -->|"Command nach unten
    RequestPeerHangup"| G

    G -->|"ATH / CLCC / CHUP
    Event nach oben: Ended"| M
    M -->|"Command nach unten
    NotifyPeerEnded"| A

```