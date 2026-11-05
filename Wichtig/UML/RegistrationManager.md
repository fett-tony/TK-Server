```mermaid
flowchart TB

    classDef box fill:#000000,stroke:#000000,color:#ff2020,font-weight:bold;
    classDef note fill:#000000,stroke:#000000,color:#ff2020,font-weight:bold,text-align:left;

    RM["TKBase_RegistrationManager.cs"]:::box

    subgraph SIPEXT[" "]
        direction TB
        R1["TKSipExt_Registra.cs"]:::box
    end

    subgraph GSM[" "]
        direction TB
        R2["TKGSM_Registra.cs"]:::box
    end

    subgraph SIPINT[" "]
        direction TB
        R3["TKSipInt_Registra.cs"]:::box
    end

    RM <--> R1
    RM <--> R2
    RM <--> R3

    TXT["zentraler neutraler Koordinator für die Registrierungsbereiche SipExt, SipInt und GSM.<br/>startet, stoppt, resetet und überwacht die jeweiligen Registra.<br/>liefert bereichsbezogene Status-Snapshots und spiegelt Runtime-Registrierungszustände.<br/>keine eigene SIP-REGISTER-Protokolllogik, keine Session-, Leg- oder Gesprächssteuerung."]:::note

    R1 ~~~ TXT
    R2 ~~~ TXT
    R3 ~~~ TXT

    style SIPEXT fill:#ffffff,stroke:#000000,stroke-width:2px
    style GSM fill:#ffffff,stroke:#000000,stroke-width:2px
    style SIPINT fill:#ffffff,stroke:#000000,stroke-width:2px

    linkStyle 0,1,2 stroke:#0000ff,stroke-width:4px

```