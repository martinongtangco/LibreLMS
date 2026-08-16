# Tasks: Fix SCORM Launch Page (Spec 031)

**Input**: Design documents from `/specs/031-fix-scorm-launch-page/`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 = enrolled learner launches an associated SCORM course from the UI

## Implementation

### US1 — UI launch works end to end

- [x] T001 [P] [US1] Fix `src/Host/Pages/Scorm/Launch.cshtml.cs`: build the launch API URI absolutely from `Request.Scheme`/`Request.Host`, forward the `Cookie` header on the server-side POST (`HttpRequestMessage` + `TryAddWithoutValidation`), add `[Authorize]` to `ScormLaunchModel`, and log the exception in the catch block before setting the generic error (FR-001…FR-004). Keep the 403/400/other error mapping and success-path field assignment unchanged.
- [x] T002 [P] [US1] Add `tests/Playwright.Tests/tests/15-scorm-launch-ui.spec.ts` per plan.md: login via `authFixture`, flush stale Valkey SCORM sessions, open `/Scorm/Launch/{SCORM_COURSE_ID}`, assert `iframe.scorm-frame` visible with `/scorm-content/` src + no `.scorm-error-page` + "SCORM Session Active" bar, assert a new `in-progress` attempt via `/api/scorm/attempts/my`, and finish the session in cleanup (FR-005).

### Verification gates (Principle XIII — sequential, parent-owned)

- [x] T003 [US1] Integrate T001+T002 on the branch; `dotnet build` the solution (show output); restart the Host app per the `restart-host-app` skill (show the "Now listening" log line); confirm `GET /Scorm/Launch/{courseId}` renders the iframe, not the error page.
- [x] T004 [US1] Run the new spec + `14-profile-courses.spec.ts` with Playwright against the running app; show passing output. Then run the full Playwright suite; show passing output (fix any regressions).
- [x] T005 [US1] Run `dotnet test tests/ArchitectureTests` (Principle III gate); merge the branch to `master`; post-merge: rebuild, restart, re-run the Playwright suite; show passing output; return to `master` (Principle XII). Mark this spec/tasks complete.

## Dependencies

- T003 depends on T001 + T002.
- T004 depends on T003.
- T005 depends on T004.

## Parallelism (Principle XI)

- T001 and T002 touch disjoint files → dispatch as parallel subagent runs
  (`runs.all`, `worktree: true`); the parent session is the sole integrator/writer for
  the shared branch and owns all verification gates.
