# 0003: Polyglot Storage — MSSQL for Records, Valkey for Live SCORM Session State

## Status
Accepted — 2026-07-28

## Context
The user asked for polyglot storage (MS SQL + Redis/NoSQL) as part of the learning goal. Rather
than bolt on a second store for its own sake, we picked a use case that actually needs different
storage semantics: a SCORM course attempt writes its `cmi.*` runtime data model (bookmark
position, quiz state in `cmi.suspend_data`, elapsed time, current status) on nearly every user
interaction via `LMSSetValue`, but only needs to be durably committed on `LMSCommit`/`LMSFinish`.
Writing every keystroke-level update straight to MSSQL would be wasteful; keeping it only in
server memory would lose it on a restart mid-attempt.

## Decision
- **MSSQL** is the system of record for everything that must survive and be queried relationally:
  users, courses, enrollments, and the final committed completion/score per attempt.
- **Valkey** (a Redis-protocol-compatible, BSD-3-licensed fork — chosen over Redis itself because
  Redis Ltd.'s 2024 relicensing to a source-available/AGPL model is a real constraint for a project
  with no reason to accept it, and the client (`StackExchange.Redis`) works against Valkey
  unchanged) holds the live `cmi.*` key/value bag for an *in-progress* SCORM attempt only. On
  `LMSCommit`/`LMSFinish` the Application layer reads the bag back out of Valkey and writes the
  durable parts (status, score, elapsed time) into MSSQL.
- MSSQL's data directory is a **named Docker volume**, not a bind mount, because bind-mounting
  `/var/opt/mssql` is a documented failure mode on Docker Desktop's WSL2 backend (intermittent
  "operating system error 31" on file modification).

## Consequences
- Nothing lives permanently only in Valkey — if it's lost on a cache flush before commit, the
  worst case is an in-progress attempt has to restart from its last committed checkpoint, not
  silent data loss of something the system considered "saved."
- This means Application-layer code for the Scorm module needs both an MSSQL-backed repository
  *and* a Valkey-backed session store, with an explicit "flush the bag to SQL" step — a small but
  real complexity cost, justified because it mirrors what a production SCORM-hosting LMS actually
  needs to do (SCORM's whole "resume where I left off" UX depends on distinguishing frequently
  written in-flight state from durably committed state).
- If a future slice needs relational guarantees on something currently modeled as Valkey-only, that
  data moves to MSSQL — Valkey does not become a second system of record by drift.
