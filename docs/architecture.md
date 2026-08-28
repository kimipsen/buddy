# Architecture

C4 context and container diagrams for Buddy. For the backend's domain model,
see [Backend domain model diagram](backend/analysis/domain-model-diagram.md)
and [Aggregate roots and their relationships](backend/analysis/aggregate-roots.md).

## System context

Two kinds of people use Buddy — guardians and children — and it depends on
three systems it doesn't own: Keycloak for identity, an SMTP relay for
transactional email, and any calendar app a guardian points at one of
Buddy's iCal feeds.

```mermaid
flowchart TB
    guardian["👤 Guardian<br/>Parent or caregiver"]:::person
    child["👤 Child<br/>Follows their day"]:::person

    subgraph boundary["Buddy"]
        buddy["Buddy<br/>Family coordination system<br/>Calendars, medicine, meals, pickups, progress"]:::system
    end

    keycloak["Keycloak<br/>Identity provider"]:::external
    smtp["SMTP relay<br/>Sends verification & invite email"]:::external
    icalClients["Calendar apps<br/>Subscribe to iCal feeds"]:::external

    guardian -- "plans routines,<br/>manages children & groups" --> buddy
    child -- "views today,<br/>marks tasks & doses done" --> buddy
    buddy -- "OIDC login, token validation,<br/>admin API" --> keycloak
    buddy -- "sends email" --> smtp
    buddy -- "serves token-scoped .ics feeds" --> icalClients

    classDef person fill:#B96A26,stroke:#7E4818,color:#ffffff;
    classDef system fill:#1F6F63,stroke:#123F38,color:#ffffff;
    classDef external fill:#7C8A85,stroke:#57635F,color:#ffffff;
```

## Containers

Inside the boundary, Buddy is three containers: an Angular SPA guardians and
children use directly, a .NET API that validates and executes every command,
and a PostgreSQL database holding Marten's append-only event streams and the
read-side documents projected from them.

```mermaid
flowchart TB
    guardian["👤 Guardian"]:::person
    child["👤 Child"]:::person

    subgraph boundary["Buddy"]
        spa["Angular SPA<br/>Frontend — guardian & child web app"]:::system
        api["Buddy API<br/>ASP.NET Core + Wolverine — endpoints & command handlers"]:::system
        db[("PostgreSQL<br/>Marten event store — streams & read documents")]:::store
    end

    keycloak["Keycloak<br/>Identity provider"]:::external
    smtp["SMTP relay<br/>Mailpit in dev, real relay in production"]:::external
    icalClients["Calendar apps<br/>iCal subscribers"]:::external

    guardian -- "uses" --> spa
    child -- "uses" --> spa
    spa -- "JSON over HTTPS" --> api
    spa -- "OIDC login, PKCE" --> keycloak
    api -- "validates bearer tokens,<br/>admin API" --> keycloak
    api -- "appends & reads events" --> db
    api -- "sends email" --> smtp
    api -- "serves token-scoped .ics feeds" --> icalClients

    classDef person fill:#B96A26,stroke:#7E4818,color:#ffffff;
    classDef system fill:#1F6F63,stroke:#123F38,color:#ffffff;
    classDef store fill:#14524A,stroke:#0B2E29,color:#ffffff;
    classDef external fill:#7C8A85,stroke:#57635F,color:#ffffff;
```

In production, Caddy sits in front of all three domains for TLS termination
and routing (see [`deploy/Caddyfile`](../deploy/Caddyfile)) — omitted above
since it doesn't change what the containers do. The dev container also
provisions RabbitMQ and Redis for future use, but no code path talks to
either yet, so they're left off this diagram too.
