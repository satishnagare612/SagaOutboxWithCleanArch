# Saga and Outbox with Clean Architecture

A .NET 10 order workflow built from five independently deployable processes (Order API, Orchestrator,
Inventory, Payment, Shipping), using the **saga** (orchestration) and **transactional outbox / inbox** patterns
over Azure Service Bus and SQL Server. Each service owns its own database and outbox.

---

## Credits

This project was built end-to-end using AI coding assistants—specifically **Deep Seek** (via Web interface), **GitHub Copilot** (via Visual Studio Community Edition) and **Google Antigravity** (via Visual Studio Code Extension).

Not a single line of code was written by a human. All system architecture, development, clean-architecture refactoring, automated tests, and documentation were produced entirely through AI assistants.

Whole effort took around four hours. The process followed is as below:

1. Idea phase -> Interact with **Deep Seek** to establish broader expectations. Once refined get optimized tokens from **Deep Seek**
2. Architecture phase -> Using **GitHub Copilot** to create basic structure and  working model
3. Implementation phase -> Through interactive sessions and chats refine the idea.
4. Final signoff and verify -> Give final touch and refactoring using **Google Antigravity**
5. Documentation phase -> Using **Google Antigravity** create diagrams and documentation

---

## Solution layout

```
src/
  SagaOutBoxCleanArch.Domain          Entities and status constants. No dependencies.
  SagaOutBoxCleanArch.Contracts       Wire contracts shared between services (messages, MessageTypes, status response). No dependencies.
  SagaOutBoxCleanArch.Application     Use cases and message handlers, plus the abstractions they need
                             (repositories, IOutbox / IInbox / IUnitOfWork, options). Depends on Domain + Contracts.
  SagaOutBoxCleanArch.Infrastructure  EF Core DbContexts + migrations, repositories, Service Bus consumer, outbox dispatcher,
                             DI wiring. Depends on Application + Domain.
  SagaOutBoxCleanArch.Api             Order REST API (host)            -> Application, Infrastructure
  SagaOutBoxCleanArch.Orchestrator    Saga orchestrator + status API   -> Application, Infrastructure
  SagaOutBoxCleanArch.Inventory       Inventory worker (host)          -> Application, Infrastructure
  SagaOutBoxCleanArch.Payment         Payment worker (host)            -> Application, Infrastructure
  SagaOutBoxCleanArch.Shipping        Shipping worker (host)           -> Application, Infrastructure
tests/
  SagaOutBoxCleanArch.Tests           Handler/service tests (EF InMemory) and architecture rules
diagrams/
  Standalone Mermaid architectural diagram files (.mmd)
```

Dependencies point inward only: `Hosts -> Infrastructure -> Application -> Domain`. `ArchitectureTests` enforce that
Domain and Contracts reference no other project, and that Application references neither Infrastructure,
EF Core nor Service Bus.

Inside each layer, code is grouped by module (`Orders`, `Sagas`, `Inventory`, `Payments`, `Shipping`) with shared
messaging code in `Messaging`.

### How the pieces fit

- **Handlers** (`Application/<Module>/*CommandHandler`, `SagaMessageHandler`) derive from `IdempotentMessageHandler`,
  which does the inbox check, runs the handler, records the inbox entry and commits once through `IUnitOfWork`.
  Business state, inbox row and outgoing outbox rows therefore commit in one transaction.
- **`ServiceDbContext`** (Infrastructure) is the shared base context. It owns the `OutboxMessages` / `InboxMessages`
  tables and implements `IOutbox`, `IInbox` and `IUnitOfWork`; each service's context derives from it.
- **Each host** calls `Add<Module>Application()` and `Add<Module>Infrastructure(configuration)` in `Program.cs`,
  and nothing else is needed to wire it.
- **Config** is bound to `WorkflowOptions` (`Workflow:*`) and `QueueNamesOptions` (`ServiceBus:*Queue`) instead of
  handlers reading `IConfiguration` directly. Configuration keys are unchanged.
- **Message subjects** are constants in `SagaOutBoxCleanArch.Contracts.MessageTypes`.

---

## Architectural Diagrams

The solution includes five complementary architectural perspectives modeling its static structure, deployment topology, data integrity mechanisms, and distributed workflow. Raw diagram files are maintained in the [`diagrams/`](diagrams/) directory.

### 1. Clean Architecture Dependencies (Onion View)
> Source: [`diagrams/1-clean-architecture-dependencies.mmd`](diagrams/1-clean-architecture-dependencies.mmd)

Enforces the inward dependency rule where business entities and use cases are independent of UI, database, and message broker implementations.

```mermaid
flowchart TD
    %% Clean Architecture Layer Boundaries
    subgraph PresentationHost ["Presentation & Hosts Layer"]
        API["SagaOutBoxCleanArch.Api<br/>(Order REST Endpoints & Controllers)"]
        ORCH["SagaOutBoxCleanArch.Orchestrator<br/>(Saga Host & Status API)"]
        INV["SagaOutBoxCleanArch.Inventory<br/>(Inventory Worker Host)"]
        PAY["SagaOutBoxCleanArch.Payment<br/>(Payment Worker Host)"]
        SHIP["SagaOutBoxCleanArch.Shipping<br/>(Shipping Worker Host)"]
    end

    subgraph Infrastructure ["Infrastructure Layer"]
        DBC["EF Core DbContexts & Migrations<br/>(ServiceDbContext, Module DbContexts)"]
        DISP["OutboxDispatcher&lt;TContext&gt;<br/>(UPDLOCK, READPAST Lease Dispatcher)"]
        BUS["Azure Service Bus Integration<br/>(QueueMessageWorker, ServiceBusClient)"]
        REPOS_INFRA["Repository Implementations<br/>(Order, Saga, Stock, Payment, Shipment)"]
    end

    subgraph Application ["Application Layer (Use Cases & Ports)"]
        PORTS["Core Messaging Ports<br/>(IOutbox, IInbox, IUnitOfWork)"]
        HANDLERS["Command & Saga Handlers<br/>(IdempotentMessageHandler, SagaMessageHandler)"]
        REPO_IF["Repository Interfaces<br/>(IOrderRepository, ISagaRepository, etc.)"]
        OPTIONS["Configuration Options<br/>(WorkflowOptions, QueueNamesOptions)"]
    end

    subgraph Domain ["Domain Layer (Enterprise Core)"]
        ENTITIES["Domain Entities<br/>(Order, SagaInstance, InventoryStock,<br/>PaymentRecord, ShipmentRecord)"]
        STATUSES["Domain Status Enums & Constants<br/>(OrderStatuses, SagaStatuses)"]
    end

    subgraph Contracts ["Shared Contracts (Wire Protocol)"]
        WIRE["Wire Messages & Types<br/>(MessageTypes, WorkflowCommand, SagaReply)"]
    end

    %% Dependency Rules (Pointing Inward)
    PresentationHost --> Application
    PresentationHost --> Infrastructure

    Infrastructure --> Application
    Infrastructure --> Domain

    Application --> Domain
    Application --> Contracts

    Infrastructure --> Contracts
    PresentationHost --> Contracts
```

---

### 2. Distributed Container & Queue Topology
> Source: [`diagrams/2-distributed-container-topology.mmd`](diagrams/2-distributed-container-topology.mmd)

Shows independently deployable processes, per-service isolated SQL Server database boundaries, and Azure Service Bus queues.

```mermaid
flowchart LR
    Client(["HTTP Client / Browser"]) -->|"POST /api/orders<br/>GET /api/orders"| ApiHost["Order API Host<br/>(SagaOutBoxCleanArch.Api)"]
    Client -->|"GET /api/sagas/{id}<br/>GET /health"| OrchHost["Saga Orchestrator<br/>(SagaOutBoxCleanArch.Orchestrator)"]

    subgraph ServiceDatabases ["Service Database Boundary (SQL Server)"]
        OrderDB[("Order DB<br/>• Orders<br/>• OutboxMessages<br/>• InboxMessages")]
        SagaDB[("Saga DB<br/>• Sagas<br/>• OutboxMessages<br/>• InboxMessages")]
        InvDB[("Inventory DB<br/>• Stocks & Reservations<br/>• OutboxMessages<br/>• InboxMessages")]
        PayDB[("Payment DB<br/>• Payments<br/>• OutboxMessages<br/>• InboxMessages")]
        ShipDB[("Shipping DB<br/>• Shipments<br/>• OutboxMessages<br/>• InboxMessages")]
    end

    subgraph Broker ["Message Broker (Azure Service Bus Queues)"]
        Q_OrderCmd["order-commands"]
        Q_SagaStart["saga-start"]
        Q_SagaReply["saga-replies"]
        Q_InvCmd["inventory-commands"]
        Q_PayCmd["payment-commands"]
        Q_ShipCmd["shipping-commands"]
    end

    subgraph Workers ["Independent Worker Hosts"]
        InvWorker["Inventory Worker<br/>(SagaOutBoxCleanArch.Inventory)"]
        PayWorker["Payment Worker<br/>(SagaOutBoxCleanArch.Payment)"]
        ShipWorker["Shipping Worker<br/>(SagaOutBoxCleanArch.Shipping)"]
    end

    %% API Host connections
    ApiHost <-->|ACID Read/Write| OrderDB
    ApiHost -.->|Outbox Dispatch| Q_OrderCmd
    ApiHost -.->|Outbox Dispatch| Q_SagaStart
    Q_OrderCmd -->|Queue Consumer| ApiHost

    %% Orchestrator connections
    OrchHost <-->|ACID Read/Write| SagaDB
    Q_SagaStart -->|Queue Consumer| OrchHost
    Q_SagaReply -->|Queue Consumer| OrchHost
    OrchHost -.->|Outbox Dispatch| Q_InvCmd
    OrchHost -.->|Outbox Dispatch| Q_PayCmd
    OrchHost -.->|Outbox Dispatch| Q_ShipCmd
    OrchHost -.->|Outbox Dispatch| Q_OrderCmd

    %% Inventory Worker connections
    InvWorker <-->|ACID Read/Write| InvDB
    Q_InvCmd -->|Queue Consumer| InvWorker
    InvWorker -.->|Outbox Dispatch| Q_SagaReply

    %% Payment Worker connections
    PayWorker <-->|ACID Read/Write| PayDB
    Q_PayCmd -->|Queue Consumer| PayWorker
    PayWorker -.->|Outbox Dispatch| Q_SagaReply

    %% Shipping Worker connections
    ShipWorker <-->|ACID Read/Write| ShipDB
    Q_ShipCmd -->|Queue Consumer| ShipWorker
    ShipWorker -.->|Outbox Dispatch| Q_SagaReply
```

---

### 3. Transactional Outbox & Idempotent Inbox Pattern
> Source: [`diagrams/3-transactional-outbox-inbox.mmd`](diagrams/3-transactional-outbox-inbox.mmd)

Eliminates the dual-write problem by saving business entity changes, outbox rows, and inbox records within a single ACID transaction, combined with an asynchronous leased dispatcher (`OutboxDispatcher<TContext>`).

```mermaid
flowchart TD
    subgraph Step1 ["Phase 1: Inbound Message Receipt & Inbox Check"]
        MsgIn["Inbound Service Bus Message<br/>(MessageId, Subject, Body)"] --> ExistsCheck{"inbox.ExistsAsync(MessageId)"}
        ExistsCheck -- Yes --> Skip["Duplicate Detected<br/>(Skip processing & ACK)"]
        ExistsCheck -- No --> Process["Execute IdempotentMessageHandler<br/>ProcessAsync(subject, body)"]
    end

    subgraph Step2 ["Phase 2: Local Atomic ACID Transaction (Single SaveChangesAsync)"]
        Process --> StateChange["1. Mutate Business Entity<br/>(Order / Stock / Payment / Saga)"]
        Process --> OutboxStage["2. Outbox.Enqueue(command, queue)<br/>(Pending dispatch)"]
        Process --> InboxMark["3. Inbox.MarkProcessed(MessageId)"]

        StateChange --> Commit["IUnitOfWork.SaveChangesAsync()<br/>Single Database Transaction"]
        OutboxStage --> Commit
        InboxMark --> Commit

        Commit --> SQLTables[("SQL Server Local DB<br/>• Domain Entity Table<br/>• OutboxMessages Table<br/>• InboxMessages Table")]
    end

    subgraph Step3 ["Phase 3: Asynchronous Leased Outbox Dispatcher (OutboxDispatcher&lt;TContext&gt;)"]
        SQLTables -.-> Poll["Background Polling Loop<br/>(Every PollIntervalSeconds)"]
        Poll --> Claim["Atomic Batch Claim SQL Query<br/>WITH (UPDLOCK, READPAST, ROWLOCK)<br/>WHERE ProcessedAt IS NULL<br/>SET LeaseId = GUID, LeaseExpiresAt = UtcNow + LeaseDuration"]
        Claim --> SendBus["Send Batch to Azure Service Bus<br/>(Sender per queue)"]
        SendBus --> MarkProcessed["Mark Dispatched SQL Query<br/>UPDATE OutboxMessages<br/>SET ProcessedAt = UtcNow<br/>WHERE Id = MessageId AND LeaseId = GUID"]
        SendBus -- Error / Timeout --> RetryBackoff["Increment RetryCount &<br/>Set NextAttemptAt (Exponential Backoff)<br/>If &gt; MaxRetries: IsDeadLetter = 1"]
    end
```

---

### 4. Saga Orchestration & Compensation Sequence
> Source: [`diagrams/4-saga-orchestration-sequence.mmd`](diagrams/4-saga-orchestration-sequence.mmd)

Chronological message flow depicting order placement, forward execution across services, and reverse-order compensation upon a step failure.

```mermaid
sequenceDiagram
    autonumber
    actor Client as HTTP Client
    participant API as Order API (SagaOutBoxCleanArch.Api)
    participant Bus as Azure Service Bus
    participant ORCH as Saga Orchestrator
    participant INV as Inventory Worker
    participant PAY as Payment Worker
    participant SHIP as Shipping Worker

    Note over Client, API: 1. Order Initiation
    Client->>API: POST /api/order (ProductId, Qty, UnitPrice)
    Note over API: DB Transaction: Save Order (Pending) + Outbox(CreateOrderCommand, StartSaga)
    API-->>Client: 201 Created (OrderDto, Location)
    API->>Bus: Dispatch Outbox: CreateOrderCommand -> order-commands
    API->>Bus: Dispatch Outbox: StartSaga -> saga-start

    Note over Bus, ORCH: 2. Saga Lifecycle Initialization
    Bus->>ORCH: StartSaga
    Note over ORCH: DB Transaction: Save SagaInstance (AwaitingOrder)
    Bus->>API: CreateOrderCommand
    Note over API: DB Transaction: Confirm OrderCreated + Outbox(Reply: OrderCreated)
    API->>Bus: Dispatch: Reply: OrderCreated -> saga-replies

    Note over Bus, ORCH: 3. Forward Workflow: Inventory Reservation
    Bus->>ORCH: Reply: OrderCreated (Success)
    Note over ORCH: Update Saga (Status: ReservingInventory) + Outbox(ReserveInventory)
    ORCH->>Bus: Dispatch: ReserveInventory -> inventory-commands
    Bus->>INV: ReserveInventory
    Note over INV: DB Transaction: Decrement Stock, Add Reservation + Outbox(Reply: ReserveInventory)
    INV->>Bus: Dispatch: Reply: ReserveInventory (Success) -> saga-replies

    Note over Bus, ORCH: 4. Forward Workflow: Payment Processing
    Bus->>ORCH: Reply: ReserveInventory (Success)
    Note over ORCH: Update Saga (Status: ProcessingPayment) + Outbox(ProcessPayment)
    ORCH->>Bus: Dispatch: ProcessPayment -> payment-commands
    Bus->>PAY: ProcessPayment
    Note over PAY: DB Transaction: Record Payment + Outbox(Reply: ProcessPayment)
    PAY->>Bus: Dispatch: Reply: ProcessPayment (Success) -> saga-replies

    Note over Bus, ORCH: 5. Forward Step Fails: Arrange Shipping
    Bus->>ORCH: Reply: ProcessPayment (Success)
    Note over ORCH: Update Saga (Status: ArrangingShipping) + Outbox(ArrangeShipping)
    ORCH->>Bus: Dispatch: ArrangeShipping -> shipping-commands
    Bus->>SHIP: ArrangeShipping
    Note over SHIP: Shipping fails (e.g., No Carrier / Validation) + Outbox(Reply: ArrangeShipping, Success=False)
    SHIP->>Bus: Dispatch: Reply: ArrangeShipping (Success=False) -> saga-replies

    Note over ORCH, Bus: 6. Reverse Compensation Execution
    Bus->>ORCH: Reply: ArrangeShipping (Failed)
    Note over ORCH: Plan: [RefundPayment, ReleaseInventory, CancelOrder]<br/>Status: Compensating
    ORCH->>Bus: Dispatch: RefundPayment -> payment-commands
    Bus->>PAY: RefundPayment
    Note over PAY: DB Transaction: Process Refund + Outbox(Reply: RefundPayment)
    PAY->>Bus: Dispatch: Reply: RefundPayment (Success) -> saga-replies

    Bus->>ORCH: Reply: RefundPayment (Success)
    ORCH->>Bus: Dispatch: ReleaseInventory -> inventory-commands
    Bus->>INV: ReleaseInventory
    Note over INV: DB Transaction: Restore Stock + Outbox(Reply: ReleaseInventory)
    INV->>Bus: Dispatch: Reply: ReleaseInventory (Success) -> saga-replies

    Bus->>ORCH: Reply: ReleaseInventory (Success)
    ORCH->>Bus: Dispatch: CancelOrder -> order-commands
    Bus->>API: CancelOrder
    Note over API: DB Transaction: Order.Status = Cancelled + Outbox(Reply: CancelOrder)
    API->>Bus: Dispatch: Reply: CancelOrder (Success) -> saga-replies

    Bus->>ORCH: Reply: CancelOrder (Success)
    Note over ORCH: All compensation steps completed.<br/>Status: Failed (Terminal Compensated)
```

---

### 5. Saga State Machine Lifecycle
> Source: [`diagrams/5-saga-state-machine.mmd`](diagrams/5-saga-state-machine.mmd)

Models the discrete lifecycle states of `SagaInstance` as defined in `SagaStatuses`.

```mermaid
stateDiagram-v2
    [*] --> AwaitingOrder : StartSaga

    state ForwardFlow {
        AwaitingOrder --> ReservingInventory : Reply(OrderCreated, Success=True)
        ReservingInventory --> ProcessingPayment : Reply(ReserveInventory, Success=True)
        ProcessingPayment --> ArrangingShipping : Reply(ProcessPayment, Success=True)
        ArrangingShipping --> Completed : Reply(ArrangeShipping, Success=True)
    }

    state FailureOrCancellation {
        AwaitingOrder --> Compensating : OrderFailed or CancelSaga
        ReservingInventory --> Compensating : Reply(ReserveInventory, Success=False) or CancelSaga
        ProcessingPayment --> Compensating : Reply(ProcessPayment, Success=False) or CancelSaga
        ArrangingShipping --> Compensating : Reply(ArrangeShipping, Success=False) or CancelSaga
    }

    state Compensating {
        [*] --> ExecuteNextStep : BeginCompensation (Build Reverse Plan)
        ExecuteNextStep --> ExecuteNextStep : Step Succeeded -> Advance CompensationIndex
        ExecuteNextStep --> RetryStep : Step Failed (RetryCount <= MaxCompensationRetries)
        RetryStep --> ExecuteNextStep : Re-enqueue Step Command
        ExecuteNextStep --> ManualIntervention : Step Failed (RetryCount > MaxCompensationRetries)
        ExecuteNextStep --> Failed : All Plan Steps Completed (Index == Plan.Count)
    }

    Completed --> [*] : Terminal Success
    Failed --> [*] : Terminal Compensated
    ManualIntervention --> [*] : Terminal Alert (Requires Operator Action)
```

---

## Run locally

Each service needs `ConnectionStrings:ServiceDatabase` (SQL Server) and Service Bus configuration
(`ServiceBus:Namespace` or `ServiceBus:ConnectionString`; use user-secrets, never commit credentials).
Provision queues `order-commands`, `inventory-commands`, `payment-commands`, `shipping-commands`, `saga-replies`
and `saga-start`.

```powershell
dotnet restore SagaOutboxWithCleanArch.sln
dotnet run --project src/SagaOutBoxCleanArch.Api
dotnet run --project src/SagaOutBoxCleanArch.Orchestrator
dotnet run --project src/SagaOutBoxCleanArch.Inventory
dotnet run --project src/SagaOutBoxCleanArch.Payment
dotnet run --project src/SagaOutBoxCleanArch.Shipping
dotnet test SagaOutboxWithCleanArch.sln
```

Without Service Bus configuration the Order API still works and stores outbox rows; it logs a warning and does not
dispatch them. The other hosts require Service Bus.

## EF Core migrations

All DbContexts live in `SagaOutBoxCleanArch.Infrastructure`, with migrations under
`Persistence/<Module>/Migrations`. Use the Infrastructure project as the migrations project and the owning host
as the startup project (each host registers only its own context):

```powershell
dotnet ef migrations add <Name> --project src/SagaOutBoxCleanArch.Infrastructure --startup-project src/SagaOutBoxCleanArch.Api          --context OrderDbContext     --output-dir Persistence/Orders/Migrations
dotnet ef migrations add <Name> --project src/SagaOutBoxCleanArch.Infrastructure --startup-project src/SagaOutBoxCleanArch.Orchestrator --context SagaDbContext      --output-dir Persistence/Sagas/Migrations
dotnet ef migrations add <Name> --project src/SagaOutBoxCleanArch.Infrastructure --startup-project src/SagaOutBoxCleanArch.Inventory     --context InventoryDbContext --output-dir Persistence/Inventory/Migrations
dotnet ef migrations add <Name> --project src/SagaOutBoxCleanArch.Infrastructure --startup-project src/SagaOutBoxCleanArch.Payment       --context PaymentDbContext   --output-dir Persistence/Payments/Migrations
dotnet ef migrations add <Name> --project src/SagaOutBoxCleanArch.Infrastructure --startup-project src/SagaOutBoxCleanArch.Shipping      --context ShippingDbContext  --output-dir Persistence/Shipping/Migrations
dotnet ef database update      --project src/SagaOutBoxCleanArch.Infrastructure --startup-project src/SagaOutBoxCleanArch.Api          --context OrderDbContext
```

## API & HTTP Endpoints

The solution exposes HTTP endpoints across two independent web processes. Each project includes a pre-configured `.http` file for testing directly in your IDE (Visual Studio, VS Code REST Client, JetBrains Rider).

### 1. Order REST API (`SagaOutBoxCleanArch.Api`)
- **Default Port**: `http://localhost:5249` (HTTPS: `https://localhost:7032`)
- **HTTP Client File**: [`src/SagaOutBoxCleanArch.Api/SagaOutBoxCleanArch.Api.http`](src/SagaOutBoxCleanArch.Api/SagaOutBoxCleanArch.Api.http)

| Method | Endpoint | Description |
|:---|:---|:---|
| `POST` | `/api/order` | Create a new order; atomically writes `Order` (Pending), `CreateOrderCommand`, and `StartSaga` outbox rows. |
| `GET` | `/api/order` | Retrieve the 100 most recent orders. |
| `GET` | `/api/order/{id}` | Retrieve a specific order by its unique GUID. |
| `POST` | `/api/order/{id}/cancel` | Cancel an order and stage a `CancelSaga` message to initiate reverse compensation. |
| `GET` | `/health` | Health check probe (returns `{"status":"healthy"}`). |

### 2. Saga Orchestrator (`SagaOutBoxCleanArch.Orchestrator`)
- **Default Port**: `http://localhost:5250` (HTTPS: `https://localhost:7033`)
- **HTTP Client File**: [`src/SagaOutBoxCleanArch.Orchestrator/SagaOutBoxCleanArch.Orchestrator.http`](src/SagaOutBoxCleanArch.Orchestrator/SagaOutBoxCleanArch.Orchestrator.http)

| Method | Endpoint | Description |
|:---|:---|:---|
| `GET` | `/api/sagas/{id}` | Query the real-time saga instance state, current status (`AwaitingOrder`, `Completed`, `Compensating`, etc.), and completed steps. |
| `GET` | `/health` | Health check probe (returns `{"status":"healthy"}`). |

## Workflow behaviour

Order created → reserve inventory → process payment → arrange shipping. A failed step triggers reverse-order
compensation (cancel shipping, refund, release inventory, cancel order, as applicable); repeated compensation
failure moves the saga to `ManualIntervention`. Delivery is at-least-once: handlers are idempotent through the
inbox table and saga state ignores stale or duplicate replies. The outbox dispatcher claims rows with
`UPDLOCK, READPAST` and a lease, so multiple replicas are safe.

Simulation switches: `Workflow:DefaultAvailableQuantity`, `Workflow:DeclinedProductPrefix` (default `DECLINE-`),
`Workflow:ShippingFailureProductPrefix`, `Workflow:MaxCompensationRetries`.
