```mermaid
flowchart TB

    classDef box fill:#000000,stroke:#000000,color:#ff2020,font-weight:bold;
    classDef note fill:#000000,stroke:#000000,color:#ff2020,font-weight:bold,text-align:left;

    RM["TKBase_RoutingManager.cs"]:::box

    subgraph SIPINT[" "]
        direction TB
        R1["TKSipInt_Routing.cs"]:::box
    end

    subgraph SVC[" "]
        direction TB
        R2["TKSVC_Routing.cs"]:::box
    end

    subgraph SIPEXT[" "]
        direction TB
        R3["TKSipExt_Routing.cs"]:::box
    end

    subgraph GSM[" "]
        direction TB
        R4["TKGSM_Routing.cs"]:::box
    end

    RM <--> R1
    RM <--> R2
    RM <--> R3
    RM <--> R4

    TXT["zentraler neutraler Koordinator für SipInt, SipExt, GSM und SVC.<br/>führt die Bereichsroutings unter gemeinsamer Laufzeitsicht zusammen.<br/>delegiert Zielauflösung und Routensuche an die zuständigen Bereichsmodule.<br/>keine Session-, Leg-, Transport- oder Gesprächssteuerung."]:::note

    R1 ~~~ TXT
    R2 ~~~ TXT
    R3 ~~~ TXT
    R4 ~~~ TXT

    style SIPINT fill:#ffffff,stroke:#000000,stroke-width:2px
    style SVC fill:#ffffff,stroke:#000000,stroke-width:2px
    style SIPEXT fill:#ffffff,stroke:#000000,stroke-width:2px
    style GSM fill:#ffffff,stroke:#000000,stroke-width:2px

    linkStyle 0,1,2,3 stroke:#0000ff,stroke-width:4px


```