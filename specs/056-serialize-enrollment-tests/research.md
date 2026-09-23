# Research: Enrollment.Tests Parallelization Flake (spec 056)

## 1. The race, end to end

`empty_search_is_no_filter` (tests/Enrollment.Tests/AdminListLearnersTests.cs,
`[Fact]`, no collection):

```csharp
var (emptyRows, emptyTotal) = await CallSpAsync("", null, 10, 1);   // read 1: catalog-wide
var (nullRows, nullTotal)   = await CallSpAsync(null, null, 10, 1); // read 2: catalog-wide
Assert.Equal(nullTotal, emptyTotal);
```

Both reads hit `dbo.AdminListLearners` with no filter, so both totals are the count of
**all** learner rows in the shared `LearningLms` database. The window between the two
reads is a few milliseconds of ADO.NET round-trips — exactly wide enough for a
concurrent `INSERT`/`DELETE` of filler rows to land in it.

The mutator is the sibling class with a DB lifecycle:
`AdminListEnrollmentsTests : IAsyncLifetime`. Its `InitializeAsync` deletes stale
`AdmPg032E` filler rows and then seeds 12 filler students + 5 filler courses + 1 orphan
course; its `DisposeAsync` deletes them again. Note that `AdminListLearnersTests`
itself is also `IAsyncLifetime`: it seeds 12 `AdmPg032L` filler students and deletes
them in its own setup/teardown. With class-level parallelism, any of those statements
can execute while `empty_search_is_no_filter` is between its two reads.

The decisive detail: **both classes insert their filler students one row at a time**
(`AdminListEnrollmentsTests.SeedFillerRows` loops 12 individual auto-committed
`ExecuteNonQuery` INSERTs; `AdminListLearnersTests.InitializeAsync` loops 12
`ExecuteNonQueryAsync` calls). The unfiltered `Students` total is therefore *moving*
throughout the run: a fresh database holds 5 seeded students (EnrollmentSeeder), plus
up to 12 `AdmPg032L` + 12 `AdmPg032E` filler rows appearing one by one and vanishing
again in teardown. The CI failure `Expected 25, Actual 24` is exactly one
auto-committed INSERT landing between the two reads (e.g. 5 + 12 + 7 = 24 at read 1,
5 + 12 + 8 = 25 at read 2). The dev database shows the same hazard at larger scale
(819 learners and counting — Principle XVII's "drifted dev database" case).

## 2. Why the fix is assembly-level serialization, not something else

| Option | Verdict |
|---|---|
| Assembly-level `CollectionBehavior(DisableTestParallelization = true)` | **Chosen.** House pattern — `f0ba9d6` did exactly this for Catalog.Tests for the same defect class (catalog-wide read compared across a sibling's teardown). One file, covers every present and future catalog-wide assertion in the project, makes the bad interleaving *impossible* rather than *unlikely*. |
| Loosen the assertion | Rejected. The empty≡NULL equivalence is the SP's contract (spec 042); the assertion is correct. |
| Put the two reads in one SP call / one transaction | Changes test semantics to fit the race; still racy against the sibling's writes (no isolation level involved here is serializable across the two connections). |
| Per-class `[Collection("db")]` on both classes | Works, but requires touching both files, and a third future DB class would need remembering the attribute. Assembly-level is the same guarantee with less surface. |
| Per-project database / `CollectionDefinition` isolation | Real design change (migrations, seeders, connection strings per project) for a problem eliminated by one 15-line file. Not proportionate. |

Cost precedent from Catalog.Tests: ~9s → ~14s (commit `f0ba9d6`). Enrollment.Tests
currently runs in ~8s; expect roughly the same delta.

## 3. Scorm.Tests decision — no change (evidence)

The defect class that fired in Catalog and Enrollment requires **both** of:

- (a) a class whose setup/teardown mutates shared rows, **and**
- (b) another class asserting on a **catalog-wide read** (a read whose result set
  depends on rows it does not own), typically compared across two reads.

`Scorm.Tests` has 7 classes with DB/Valkey lifecycles. Per-class evidence:

| Class | Markers | Assertion scope | (b) present? |
|---|---|---|---|
| `ConcurrentLaunchRetryTests` | random-GUID student/course per run; temp wwwroot dir | its own marker pair | no |
| `DuplicateKeyExceptionContractTests` | `Guid.NewGuid()` per test | unique-index violation for its own rows | no |
| `GetCourseIdsWithPackagesBulkTests` | 3 random-GUID courses + 1 pool package | `GetCourseIdsWithPackagesAsync` is called **with the explicit input ids**; asserts exactly those two ids come back, pool package (null course) excluded — a concurrent class's rows have other random-GUID course ids and cannot enter the result | no (single scoped read, not two compared) |
| `GetMyAttemptsBatchTests` | random-GUID marker student, cleaned up | attempts **for that student id only** | no |
| `ScormActiveIndexTests` | random-GUID marker pair; index key `scorm:active:{student}:{course}` | reads/writes only its own key; even asserts a random other student's key is null | no |
| `ScormSessionOwnershipTests` | random-GUID owner/intruder/course; temp wwwroot | its own marker pair | no |
| `ScormUploadTraversalTests` | unique rooted temp paths; no persistent DB rows asserted | filesystem round-trips under unique paths | no |

Every class scopes both its mutations **and** its assertions to per-run random-GUID
markers, so a sibling class's lifecycle can neither enter another class's read nor
change what it asserts. Condition (b) — the catalog-wide read — does not exist in the
project. That is the stated reason it has not flaked, and it is why adding
`DisableTestParallelization` now would be unproven prophylaxis with a real runtime cost
(7 lifetime classes, several of them Valkey + file round-trips).

**Standing condition**: if a Scorm.Tests flake ever surfaces with this signature
(two reads of a shared aggregate diverging), the same one-file fix applies — revisit
then, with the flake as evidence, rather than pre-emptively.

## 4. Regression guard scope (per `regression-guard-scope-check`)

The defect pattern is "catalog-wide read compared against a later/earlier catalog-wide
read while a sibling class mutates the shared table". `git grep`-equivalent sweep of
the tracked tree at 490d358:

- Catalog.Tests — already serialized (`f0ba9d6`).
- Enrollment.Tests — the instance this spec fixes.
- Scorm.Tests, Host.Tests, Management.Tests, ArchitectureTests — no catalog-wide
  read-compares (Host/Management/Architecture tests are unit-level or scoped fixtures;
  see §3 table for Scorm).

The assembly-level attribute in Enrollment.Tests therefore covers the class for the
whole project, not just the one failing test.
