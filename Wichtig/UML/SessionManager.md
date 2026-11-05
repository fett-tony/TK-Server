```mermaid
flowchart TB

    classDef box fill:#000000,stroke:#000000,color:#ff2020,font-weight:bold;
    classDef note fill:#000000,stroke:#000000,color:#ff2020,font-weight:bold,text-align:left;

    SM["TKBase_SessionManager.cs"]:::box

    subgraph AREAS[" "]
        direction LR

        subgraph GSM[" "]
            direction TB
            GMS["TKGSM_Session.cs"]:::box
            GCC["CallController"]:::box
            subgraph GSMB[" "]
                direction LR
                GMC["TKGSM_Commands.cs"]:::box
                GME["TKGSM_Events.cs"]:::box
            end
        end

        subgraph SIPEXT[" "]
            direction TB
            SES["TKSipExt_Session.cs"]:::box
            SCC["CallController"]:::box
            subgraph SIPEXTB[" "]
                direction LR
                SEC["TKSipExt_Commands.cs"]:::box
                SEE["TKSipExt_Events.cs"]:::box
            end
        end

        subgraph SIPINT[" "]
            direction TB
            SIS["TKSipInt_Session.cs"]:::box
            ICC["CallController"]:::box
            subgraph SIPINTB[" "]
                direction LR
                SIC["TKSipInt_Commands.cs"]:::box
                SIE["TKSipInt_Events.cs"]:::box
            end
        end

        subgraph SVC[" "]
            direction TB
            SVS["TKSVC_Session.cs"]:::box
            VCC["CallController"]:::box
            subgraph SVCB[" "]
                direction LR
                SVCMD["TKSVC_Commands.cs"]:::box
                SVE["TKSVC_Events.cs"]:::box
            end
        end
    end

    SM <--> GMS
    SM <--> SES
    SM <--> SIS
    SM <--> SVS

    GMS <--> GCC
    SES <--> SCC
    SIS <--> ICC
    SVS <--> VCC

    GCC --> GMC
    GMC --> GME
    GME --> GCC

    SCC --> SEC
    SEC --> SEE
    SEE --> SCC

    ICC --> SIC
    SIC --> SIE
    SIE --> ICC

    VCC --> SVCMD
    SVCMD --> SVE
    SVE --> VCC

    TXT["1 SessionManager oben und nur für Bereichsauswahl.<br/>2 Session hält Gesprächskontext und Legs.<br/>3 CallController steuert den Bereichsablauf.<br/>4 Commands gehen nach unten.<br/>5 Events kommen von unten zurück in den CallController."]:::note

    AREAS ~~~ TXT

    style GSM fill:#ffffff,stroke:#000000,stroke-width:2px
    style SIPEXT fill:#ffffff,stroke:#000000,stroke-width:2px
    style SIPINT fill:#ffffff,stroke:#000000,stroke-width:2px
    style SVC fill:#ffffff,stroke:#000000,stroke-width:2px

    style GSMB fill:#ffffff,stroke:#ffffff
    style SIPEXTB fill:#ffffff,stroke:#ffffff
    style SIPINTB fill:#ffffff,stroke:#ffffff
    style SVCB fill:#ffffff,stroke:#ffffff

    linkStyle 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19 stroke:#0000ff,stroke-width:4px


```