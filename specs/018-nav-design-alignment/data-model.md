# Data Model: Nav & Header Design Alignment

**Feature**: 018-nav-design-alignment
**Date**: 2025-08-04

## Summary

No data model changes. This feature is purely presentational — all state is client-side (role switcher preference in `localStorage`). No new entities, fields, migrations, or database changes are required.

## Client-Side State

| Key | Type | Persistence | Purpose |
|-----|------|-------------|---------|
| `nav-role-view` | `"learner"` \| `"admin"` | `localStorage` | Role switcher segment selection |
