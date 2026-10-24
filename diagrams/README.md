# Architectural Diagrams

This folder contains the Mermaid source files for the 5 architectural perspectives of the `SagaOutBoxCleanArch` solution.

## Files

1. [1-clean-architecture-dependencies.mmd](1-clean-architecture-dependencies.mmd): **Clean Architecture Dependencies (Onion / Layered View)**
   - Illustrates the inward dependency rule (`Hosts -> Infrastructure -> Application -> Domain`), core ports, adapters, and modular structure.
2. [2-distributed-container-topology.mmd](2-distributed-container-topology.mmd): **Distributed Container & Queue Topology (C4 View)**
   - Maps runtime services, per-service SQL Server databases, and Azure Service Bus queues (`order-commands`, `saga-start`, `saga-replies`, `inventory-commands`, `payment-commands`, `shipping-commands`).
3. [3-transactional-outbox-inbox.mmd](3-transactional-outbox-inbox.mmd): **Transactional Outbox & Inbox Pattern (Data Consistency View)**
   - Details how dual-writes are prevented via single ACID transactions (`IUnitOfWork.SaveChangesAsync`), at-least-once dispatch (`OutboxDispatcher<TContext>` with `UPDLOCK, READPAST`), and inbox deduplication (`IdempotentMessageHandler`).
4. [4-saga-orchestration-sequence.mmd](4-saga-orchestration-sequence.mmd): **Saga Orchestration & Compensation Sequence (Dynamic View)**
   - End-to-end temporal message flow showing happy-path forward progress and reverse-order compensation upon a step failure.
5. [5-saga-state-machine.mmd](5-saga-state-machine.mmd): **Saga State Machine (Lifecycle View)**
   - Formally specifies the lifecycle states defined in `SagaStatuses`: `AwaitingOrder`, `ReservingInventory`, `ProcessingPayment`, `ArrangingShipping`, `Completed`, `Compensating`, `Failed`, and `ManualIntervention`.

