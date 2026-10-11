# 0001. Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

This project deliberately explores design choices that matter in financial systems: consistency, concurrency, idempotency and module boundaries. The reasoning behind those choices is as valuable as the code, and it is easily lost once the code is written.

## Decision

Every significant architectural decision is recorded as an Architecture Decision Record (ADR) in `docs/adr/`, using [the template](0000-template.md). ADRs are numbered sequentially and never rewritten; a changed decision gets a new ADR that supersedes the old one.

A decision is "significant" when it is expensive to reverse, affects more than one module, or a reasonable engineer could have chosen differently.

## Alternatives considered

- **Wiki or external document:** drifts away from the code and is invisible in code review.
- **Code comments only:** explain *what*, rarely *why not the alternative*.

## Consequences

- Reviewers can read why the system looks the way it does.
- Writing an ADR forces alternatives to be weighed explicitly before code is written.
- Small ongoing cost: each decision needs a short write-up.
