# ADR-0001: CQRS and mapping without MediatR / AutoMapper; tests without FluentAssertions

- Status: Accepted
- Date: 2026-06-26

## Context

The engineering standards call for CQRS handlers, object mapping, and validation. The most common
libraries for these (MediatR, AutoMapper) and the popular assertion library FluentAssertions have all
moved to **commercial/paid licensing** for current versions. AuditX is delivered as an **on-premises
product installed inside a customer bank's network**; embedding libraries that carry per-seat or
per-organisation commercial licence obligations into the shipped product is inappropriate and creates
downstream legal/operational burden for the bank.

## Decision

- **CQRS**: implement a small in-house dispatcher (`IDispatcher` + `ICommandHandler<,>` /
  `IQueryHandler<,>`) with a FluentValidation pre-handler pipeline (`AuditX.Application.Common.Messaging`).
- **Mapping**: hand-write explicit entity → DTO mappers (`IdentityMappings`). They are trivial,
  allocation-light, AOT-friendly, and obvious to read.
- **Validation**: use **FluentValidation** (still free, MIT).
- **Tests**: use xUnit assertions + **NSubstitute** (both permissively licensed); do not use
  FluentAssertions v8+.

## Consequences

- No commercial-licence exposure in the shipped product.
- A few hundred lines of in-house infrastructure we own and can evolve.
- The standards' *intent* (CQRS, mapping, validation, structured tests) is fully met; only the specific
  libraries differ. This is recorded here so the deviation is explicit and auditable.
