# Tasks: Admin List Pagination with Page Size Toggle

**Input**: Design documents from `/specs/032-admin-pagination/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (stored-procedures.md, module-contracts.md, admin-pages-query.md), quickstart.md

**Tests**: INCLUDED — required by spec SC-005 and Constitution Principle XIII (E2E proof + regression), with the strategy fixed in research.md R8 (xUnit integration tests against live MSSQL + one self-contained Playwright spec).

**Organization**: Tasks are grouped by user story. US1/US2/US4 touch disjoint file sets and can run in parallel after Phase 2; the single serialization point is the shared Playwright spec file `tests/Playwright.Tests/tests/16-admin-pagination.spec.ts` (its tasks are deliberately not [P]).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4 from spec.md)
- Exact file paths included; contract/SP shapes referenced, not duplicated

---

## Phase 1: Setup

**Purpose**: Branch discipline (Constitution VIII) and a green baseline before any change.

- [X] T001 Create feature branch from the integration branch: `git checkout master && git checkout -b story/032-admin-pagination` (Constitution VIII)
- [X] T002 Verify green baseline: `dotnet build LibreLms.slnx` and `dotnet test tests/ArchitectureTests` both pass; capture output (Constitution XIII gate-1 evidence)

**Checkpoint**: Baseline green on `story/032-admin-pagination` — work may proceed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared UI + state primitives that ALL three admin pages consume. No user story may render pagination before these exist.

- [X] T003 [P] Create the shared admin pagination partial in `src/Host/Pages/Shared/_AdminPagination.cshtml`: (a) Previous/Next buttons that are **hidden** (not disabled) at boundaries and a whole-`<nav>` hidden when `totalPages <= 1`, (b) center indicator "Page {page} of {totalPages} ({total} total)", (c) page-size `<select name="pageSize">` with options exactly 10/30/50/100 (current value selected, submits the enclosing filter form on change), plus a hidden `pageNumber=1` reset field; parameterized by a small view model (page, totalPages, pageSize, total) and a link-builder so each page supplies its own filter/sort query params (contracts/admin-pages-query.md)
- [X] T004 [P] Create the shared page-state helper in `src/Host/Pages/Admin/AdminPageState.cs`: `NormalizePageSize(int requested)` (allowlist {10,30,50,100}, anything else → 10), `ClampPage(int requested, int totalCount, int pageSize)` (→ `max(1, min(requested, TotalPages))`), `TotalPages(int total, int pageSize)` (→ `max(1, ceil(total/pageSize))`) — pure static functions used by all three admin page models

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel (US1 ∥ US2 ∥ US4).

---

## Phase 3: User Story 1 - Paginate the Admin Enrollments List (Priority: P1) 🎯 MVP

**Goal**: Admin > Enrollments pages its rows server-side via the new `dbo.AdminListEnrollments` stored procedure with the shared pagination controls, composing with the existing student/course filters.

**Independent Test**: Seed >10 enrollments (marker-prefixed), open Admin > Enrollments as SuperUser: one page of rows + controls render, Next/Previous navigate, indicator totals correct, student/course filters narrow the paginated results and reset to page 1.

### Tests for User Story 1 (write FIRST — must FAIL before the procedure exists)

- [X] T005 [P] [US1] Create xUnit integration tests for `dbo.AdminListEnrollments` in `tests/Enrollment.Tests/AdminListEnrollmentsTests.cs` (pattern: `tests/Catalog.Tests/CourseCatalogSearchTests.cs` — live MSSQL via env connection string, marker-prefixed filler rows, clean up after): student-name filter, course-title filter, both combined, paging math (page 1/2/last), `EnrolledAt DESC, Id DESC` ordering, determinism (two identical consecutive calls return identical page boundaries), `@PageSize<=0`/`@PageNumber<=0` floors, count result set matches filtered total, inner-join omission of enrollments whose course was deleted
- [X] T006 [P] [US1] Extend `src/Modules/Enrollment.Contracts/IEnrollmentAdmin.cs`: add `Task<AdminEnrollmentPageResult> ListPagedAsync(string? studentName, string? courseTitle, int pageNumber, int pageSize)` plus records `AdminEnrollmentRow(EnrollmentId, StudentId, StudentName, StudentEmail, CourseId, CourseTitle, OrganizationId, EnrolledAt)` and `AdminEnrollmentPageResult(IList<AdminEnrollmentRow> Items, int TotalCount)` exactly per contracts/module-contracts.md
- [X] T007 [P] [US1] Extend `UserSummary` with a trailing `Guid OrganizationId` parameter in `src/Modules/Enrollment.Contracts/IUserLookup.cs` (additive; single implementor)

### Implementation for User Story 1

- [X] T008 [P] [US1] Create EF Core migration (raw `migrationBuilder.Sql`, drop/create guard pattern from `20260805020000_AddFullTextIndexAndBrowseProcedure.cs`) creating `dbo.AdminListEnrollments` in `src/Host/Migrations/Enrollment/` per contracts/stored-procedures.md §2 (join Enrollments→Students→Courses; name/title filters; `ORDER BY e.EnrolledAt DESC, e.Id DESC`; rows + `COUNT(*)` result sets)
- [X] T009 [US1] Implement `EnrollmentAdminService.ListPagedAsync` in `src/Modules/Enrollment/Application/EnrollmentAdminService.cs`: raw `SqlCommand("AdminListEnrollments")` over the `EnrollmentDbContext` connection (open/close pattern from `CourseCatalogService.BrowseAsync`), read result set 1 into `AdminEnrollmentRow` list and result set 2 into `TotalCount` (depends: T006, T008)
- [X] T010 [US1] Populate `UserSummary.OrganizationId` in `UserLookupService.GetUsersAsync` in `src/Modules/Enrollment/Application/UserLookupService.cs` (select `s.OrganizationId` in the existing batch query; depends: T007)
- [X] T011 [US1] Add `EnrollmentPageResult(IList<EnrollmentDto> Items, int TotalCount)` record and `ListAllEnrollmentsPagedAsync(studentName, courseTitle, pageNumber, pageSize)` to `src/Modules/Management/Application/AdminEnrollmentService.cs`: delegate to `IEnrollmentAdmin.ListPagedAsync`, resolve org names for the page's **distinct** `OrganizationId`s via `IOrganizationLookup.GetOrganizationAsync` with a page-local dictionary (paged path only); keep existing `ListAllEnrollmentsAsync` untouched (depends: T006, T009)
- [X] T012 [US1] Update `src/Host/Pages/Admin/Enrollments/Index.cshtml.cs`: bound `PageNumber` (default 1) and `PageSize` (default 10) normalized via `AdminPageState`, trim/null `student`/`course` filters, call `ListAllEnrollmentsPagedAsync` with the clamped page, expose `TotalCount`/`TotalPages`; in `OnPostCancelAsync` re-query the same view and, if the current page came back empty and `PageNumber > 1`, re-query `PageNumber - 1` (spec edge case) (depends: T004, T011)
- [X] T013 [US1] Update `src/Host/Pages/Admin/Enrollments/Index.cshtml`: hidden `pageNumber=1` reset in the filter form, render the T003 shared partial under the table (link builder carries `student`/`course` + current `pageSize`), keep empty-state message, nav hidden when `TotalPages <= 1` (depends: T003, T012)
- [X] T014 [US1] Create `tests/Playwright.Tests/tests/16-admin-pagination.spec.ts` (self-contained per `11-course-pagination.spec.ts` pattern: SuperUser login helper, marker-prefixed filler learners+enrollments in `beforeAll`, cleanup in `afterAll`, serial mode) with an `Admin Enrollments pagination` describe block: controls render above one page, Previous hidden on page 1 / Next hidden on last page, indicator format, filter+pagination compose, filter change resets to page 1; extend `tests/Playwright.Tests/pages/AdminEnrollmentsPage.ts` with pagination locators (depends: T012, T013; owns the shared spec file for later stories)
- [X] T015 [US1] Validate US1 (Constitution XIII evidence): `dotnet test tests/Enrollment.Tests --filter FullyQualifiedName~AdminListEnrollments`, `npx playwright test tests/Playwright.Tests/tests/16-admin-pagination.spec.ts -g "Enrollments"`, quickstart.md §4.3 manual smoke; commit (depends: T005, T009, T014)

**Checkpoint**: Admin > Enrollments is fully paginated and independently testable — **MVP**.

---

## Phase 4: User Story 2 - Paginate the Admin Learners List (Priority: P1)

**Goal**: Admin > Learners pages its rows server-side via the new `dbo.AdminListLearners` stored procedure, composing with the existing name/email search and role filter.

**Independent Test**: Seed >10 learners (marker-prefixed), open Admin > Learners as SuperUser: one page + controls, name/email search and role filter narrow the paginated results with correct totals, pagination navigates in name order.

### Tests for User Story 2 (write FIRST — must FAIL before the procedure exists)

- [X] T016 [P] [US2] Create xUnit integration tests for `dbo.AdminListLearners` in `tests/Enrollment.Tests/AdminListLearnersTests.cs` (same live-MSSQL/filler pattern): name search, email search, both, exact-role filter, search+role combined, paging math, `Name ASC, Id ASC` ordering, determinism, input floors, count accuracy, credential columns never present in the result (parallel with US1)
- [X] T017 [P] [US2] Extend `src/Modules/Enrollment.Contracts/IUserProvisioning.cs`: add `Task<StudentPageResult> ListPagedAsync(string? search, string? roleFilter, int pageNumber, int pageSize)` plus record `StudentPageResult(IList<StudentProvisionedDto> Items, int TotalCount)` per contracts/module-contracts.md

### Implementation for User Story 2

- [X] T018 [P] [US2] Create EF Core migration (raw SQL, same guard pattern) creating `dbo.AdminListLearners` in `src/Host/Migrations/Enrollment/` per contracts/stored-procedures.md §3 (Students only; `@Search` on Name OR Email, `@Role` exact; `ORDER BY s.Name ASC, s.Id ASC`; rows map 1:1 to `StudentProvisionedDto` columns; rows + count result sets)
- [X] T019 [US2] Implement `UserProvisioningService.ListPagedAsync` in `src/Modules/Enrollment/Application/UserProvisioningService.cs`: raw `SqlCommand("AdminListLearners")` over the `EnrollmentDbContext` connection, map rows to `StudentProvisionedDto` (never select credential columns), return items + count (depends: T017, T018)
- [X] T020 [US2] Add `UserPageResult(IList<UserDto> Items, int TotalCount)` record and `ListAllPagedAsync(search, roleFilter, pageNumber, pageSize)` to `src/Modules/Management/Application/UserService.cs`: delegate to `IUserProvisioning.ListPagedAsync`, resolve org names for the page's distinct `OrganizationId`s via `IOrganizationLookup` (page-local cache); keep `ListAllAsync` untouched (depends: T017, T019)
- [X] T021 [US2] Update `src/Host/Pages/Admin/Learners/Index.cshtml.cs`: bound `PageNumber`/`PageSize` normalized via `AdminPageState`, trim/null search, pass the role filter, call `ListAllPagedAsync` with the clamped page, expose `TotalCount`/`TotalPages`; the `org` param stays bound-but-unapplied (pre-existing gap, out of scope — keep the dropdown) (depends: T004, T020)
- [X] T022 [US2] Update `src/Host/Pages/Admin/Learners/Index.cshtml`: hidden `pageNumber=1` reset in the filter form, render the shared partial under the table (link builder carries `search`/`role`/`org` + current `pageSize`), keep empty state (depends: T003, T021)
- [X] T023 [US2] Append an `Admin Learners pagination` describe block to `tests/Playwright.Tests/tests/16-admin-pagination.spec.ts` (reuse US1 helpers; marker-prefixed filler learners) and extend `tests/Playwright.Tests/pages/AdminLearnersPage.ts` with pagination locators: controls render, search narrows paginated results, role filter composes, boundary control hiding (depends: T014 spec skeleton, T021, T022; serializes the shared spec file)
- [X] T024 [US2] Validate US2 (Constitution XIII evidence): `dotnet test tests/Enrollment.Tests --filter FullyQualifiedName~AdminListLearners`, the Playwright Learners block, quickstart.md §4.4 smoke; commit (depends: T016, T019, T023)

**Checkpoint**: Both P1 list pages (Enrollments, Learners) paginate independently.

---

## Phase 5: User Story 3 - Toggle Page Size on All Three Admin Pages (Priority: P1)

**Goal**: The page-size selector contract (exact options 10/30/50/100, default 10, reset-to-page-1 on change, retention while paging, invalid-value fallback) holds on the completed pages and is tamper-safe via URL.

**Independent Test**: On Admin > Enrollments and Admin > Learners: selector shows exactly 10/30/50/100 (default 10); changing it re-renders at page 1 with the chosen row count; size retained across Next/Previous; `pageSize=999`/`pageSize=15` in the URL render with size 10; `pageNumber=99999` renders the last valid page. (The Courses-page half closes in Polish, T036, after US4 lands.)

### Tests for User Story 3 (write FIRST where possible)

- [X] T025 [US3] Append an `Admin page-size toggle` describe block to `tests/Playwright.Tests/tests/16-admin-pagination.spec.ts` covering Enrollments + Learners: exact option set and default 10, change 10→50 re-renders at page 1 with ≤50 rows, size retained across Next/Previous, URL tamper `pageSize=999`→10 and `pageSize=15`→10, `pageNumber=99999`→last page, nav hidden when `TotalPages <= 1` (depends: T014, T023 blocks exist; serializes the shared spec file)
- [X] T026 [US3] Validate US3 partial (Constitution XIII evidence): run the Playwright page-size block; verify the rendered selector markup is identical between the two pages (options, labels, default, indicator format per contracts/admin-pages-query.md interaction rules 2–4); commit (depends: T025)

**Checkpoint**: Page-size toggle verified on Enrollments + Learners; the Courses-page half of US3's final acceptance closes at T036 after US4.

---

## Phase 6: User Story 4 - Bring Admin Courses Pagination in Line with the Standard (Priority: P2)

**Goal**: Admin > Courses uses the shared pagination controls + page-size toggle, with sorting applied **server-side** in the extended `dbo.BrowseCourses` procedure (fixing the in-page-only sort), and org names resolved per page instead of a full-catalog load.

**Independent Test**: Seed >10 courses with out-of-title-order categories, open Admin > Courses as SuperUser: clicking a sort header orders the whole filtered set across pages, page-size toggle works as in US3, filters compose, deleting a course steps back when the current page empties, and the public Browse Courses page is unchanged.

### Tests for User Story 4 (write FIRST — sort behavior must FAIL before the procedure change)

- [X] T027 [P] [US4] Extend `tests/Catalog.Tests/CourseCatalogSearchTests.cs` (or add `tests/Catalog.Tests/BrowseCoursesSortTests.cs` alongside) with the extended-`BrowseCourses` matrix: each of `title`/`category`/`duration` × asc/desc verified **across a page boundary** (filler courses crafted so in-page order ≠ full-set order), `OrganizationId` present in the result, original four-parameter call reproduces legacy title-ASC behavior (FR-017), whitespace-only and special-character search terms are safe, unknown `@SortBy`/`@SortDirection` fall back to defaults
- [X] T028 [P] [US4] Create EF Core migration (raw SQL, drop/create guard) extending `dbo.BrowseCourses` in `src/Host/Migrations/Catalog/` per contracts/stored-procedures.md §1: add `@SortBy NVARCHAR(20) = N'title'`, `@SortDirection NVARCHAR(4) = N'asc'` (normalized to the allowed set at the top of the procedure), add `c.OrganizationId` as result column index 5, replace ORDER BY with the six-branch `CASE` pattern + `c.Id ASC` tie-break

### Implementation for User Story 4

- [X] T029 [US4] Update `src/Modules/Catalog/Application/CourseCatalogService.cs`: `BrowseAsync` gains trailing optional `string sortBy = "title", string sortDirection = "asc"` (allowlisted in C# before the call), passes them to the procedure, reads column 5 into `CourseItemDto` (trailing `Guid OrganizationId` record parameter; the single construction site is updated) (depends: T028)
- [ ] T030 [US4] Update `src/Host/Pages/Admin/Courses/Index.cshtml.cs`: `PageSize` default 15 → 10 + `AdminPageState` allowlist/clamp, pass `SortBy`/`SortDirection` through to `BrowseAsync`, **delete `ApplySorting`** (in-memory post-page sort), replace the `GetAllCoursesAsync()` full-catalog org-name load with page-local resolution of the page's distinct `OrganizationId`s via `IOrganizationLookup` (inject `LibreLms.Modules.Management.Contracts.IOrganizationLookup`), keep the per-course SCORM lookup (page-bounded, existing pattern), step-back logic in `OnPostDeleteAsync` when the current page empties (depends: T004, T029)
- [ ] T031 [US4] Update `src/Host/Pages/Admin/Courses/Index.cshtml`: replace the inline pagination `<nav>` with the shared partial (adds the page-size select), add `pageSize` to the sort-header link queries, keep the hidden `pageNumber=1` reset and the empty state (depends: T003, T030)
- [ ] T032 [US4] Append an `Admin Courses pagination` describe block to `tests/Playwright.Tests/tests/16-admin-pagination.spec.ts` (marker-prefixed filler courses with crafted sort orders): sort header changes the **cross-page** order, page-size toggle, filter+sort+pagination compose, delete steps back on an emptied page; then run the FR-017 regression `npx playwright test tests/Playwright.Tests/tests/11-course-pagination.spec.ts` (public browse unchanged) (depends: T014, T025, T030, T031; serializes the shared spec file)
- [ ] T033 [US4] Validate US4 (Constitution XIII evidence): `dotnet test tests/Catalog.Tests`, the Playwright Courses block + `11-course-pagination` regression, quickstart.md §4.2/§4.6 smoke; commit (depends: T027, T029, T032)

**Checkpoint**: All three admin pages share one pagination standard; US3's Courses half is now verifiable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Close remaining acceptance, record the architectural decision, and satisfy the full Constitution XIII gate sequence.

- [ ] T034 [P] Write ADR `docs/adr/0008-cross-module-sql-join-for-admin-listing.md` (context → decision → consequences, ≤1 page, Constitution IV): why `AdminListEnrollments` joins the Catalog-owned `Courses` table in T-SQL (course-title filter must precede paging; single shared database; compiled module boundary intact), and what was rejected (two-phase contract fetch, title denormalization)
- [ ] T035 Full regression run (quickstart.md §1–§3): `dotnet build LibreLms.slnx`, `dotnet test` for ArchitectureTests + Catalog.Tests + Enrollment.Tests + Scorm.Tests, `npx playwright test` for specs 05, 07, 10, 11, 16 — all green, output captured (Constitution XIII gates 1–2)
- [ ] T036 Cross-page page-size consistency check closing US3's final acceptance scenario: verify identical selector options/labels/default and indicator format across all three admin pages (Playwright cross-check appended to `16-admin-pagination.spec.ts` or the quickstart.md §4 manual pass)
- [ ] T037 Merge & post-merge regression (Constitution XII + XIII gate 3): merge `story/032-admin-pagination` → `master`, `git checkout master`, rebuild, restart the Host, re-run the Playwright admin suite (05, 07, 10, 11, 16) on merged code, capture passing output; session ends on `master`

**Checkpoint**: Feature complete with all three Constitution XIII gates evidenced on merged code.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — starts immediately.
- **Foundational (Phase 2)**: Depends on Setup (green baseline). **BLOCKS all user stories** (T003/T004 are consumed by every page change).
- **User Stories (Phases 3–6)**: All depend on Phase 2.
- **Polish (Phase 7)**: Depends on all four stories (T035/T036/T037); T034 (ADR) only needs the US1 SP work (T008) and can start any time after it.

### User Story Dependencies (completion order)

- **US1 (P1, Enrollments)**: After Phase 2. No dependencies on other stories. **MVP.**
- **US2 (P1, Learners)**: After Phase 2. No dependencies on other stories — **fully parallel with US1** (disjoint files; only the shared Playwright spec serializes: T023 runs after T014).
- **US4 (P2, Courses)**: After Phase 2. No dependencies on US1/US2 (Catalog-side files are disjoint) — parallel with US1/US2; its spec block (T032) runs after T025.
- **US3 (P1, page-size toggle)**: Implementation lives in Phase 2 (T003/T004) plus each story's page wiring. Its validation phase (T025/T026) runs after US1+US2; its final cross-page acceptance (Courses) closes in Polish at T036 after US4.
- **Execution order**: US1 ∥ US2 ∥ US4 (parallel) → US3 validation → Polish.

### Within Each User Story

- Tests (T005, T016, T027) are written first and must FAIL before the corresponding procedure/implementation exists (TDD per research.md R8).
- Contracts before service implementations; migrations before implementations are validated; page model before view; spec block after view; validation task last.

### Parallel Opportunities (Constitution XI — dispatch `[P]` tasks as parallel subagent runs)

- Phase 2: T003 ∥ T004
- US1 initial fan-out: T005 ∥ T006 ∥ T007 ∥ T008 (four disjoint files: test, contract, contract, migration)
- US2 initial fan-out: T016 ∥ T017 ∥ T018
- US4 initial fan-out: T027 ∥ T028
- Story-level: US1, US2, US4 run concurrently after Phase 2 (parent session orchestrates; one writer per file set). The shared Playwright spec file is the only cross-story serialization point — its blocks are appended strictly in ID order: T014 → T023 → T025 → T032 → T036
- Polish: T034 ∥ T035 (disjoint)

---

## Parallel Example: User Story 1

```text
Wave 1 (parallel — four disjoint files):
  T005  Integration tests for AdminListEnrollments     tests/Enrollment.Tests/AdminListEnrollmentsTests.cs
  T006  IEnrollmentAdmin.ListPagedAsync + records      src/Modules/Enrollment.Contracts/IEnrollmentAdmin.cs
  T007  UserSummary + OrganizationId                   src/Modules/Enrollment.Contracts/IUserLookup.cs
  T008  AdminListEnrollments migration (raw SQL)       src/Host/Migrations/Enrollment/<ts>_AddAdminListEnrollments.cs

Wave 2 (parallel — depends on wave 1):
  T009  EnrollmentAdminService.ListPagedAsync          src/Modules/Enrollment/Application/EnrollmentAdminService.cs
  T010  UserLookupService org-id population            src/Modules/Enrollment/Application/UserLookupService.cs

Wave 3 (sequential):
  T011 -> T012 -> T013 -> T014 -> T015
  (Management facade -> page model -> view -> Playwright block -> validation + commit)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (branch + green baseline) → Phase 2 (shared partial + state helper) → Phase 3 (US1 Enrollments).
2. **STOP and VALIDATE** at the US1 checkpoint: Enrollments pages server-side with the full control set; the shared foundation is proven and the worst user-reported page is fixed.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 (Enrollments) → validate → **MVP**.
3. US2 (Learners) → validate → both P1 list pages done.
4. US4 (Courses) → validate → all three pages on one standard; Browse regression green.
5. US3 validation + Polish (T025–T037) → full spec acceptance, ADR, Constitution XIII evidence, merge.
6. Each increment leaves the app running and the existing suite green.

### Parallel Team / Subagent Strategy (Constitution XI)

1. Parent session runs Phase 1–2 (T003 ∥ T004).
2. Fan out three story owners: US1 (enrollment admin path), US2 (learner path), US4 (catalog path) — disjoint file sets; each owner follows their story's waves above.
3. Shared-file discipline: only the owning story touches `16-admin-pagination.spec.ts`, in ID order (T014 → T023 → T025 → T032 → T036); all other tasks parallelize freely.
4. Parent synthesizes at each checkpoint, runs the validation tasks, and owns the final merge (never delegated).

---

## Notes

- [P] = different files, no dependencies on incomplete tasks.
- [Story] labels map tasks to spec.md user stories for traceability.
- Each user story is independently completable and testable at its checkpoint.
- Verify tests fail before implementing (T005, T016, T027).
- Commit after each validation task (T015, T024, T026, T033, T037).
- Stop at any checkpoint to validate the increment independently.
- Avoid: vague tasks, same-file conflicts (the one shared-file exception is explicitly serialized), cross-story dependencies that break independence.
