using Xunit;

// Every class in this project reads and writes the same physical LearningLms
// database (there is no per-test isolation), and xUnit runs test classes in
// parallel by default. That lets one class's setup/teardown mutate rows in the
// middle of another class's assertion.
//
// The failure this prevents: AdminListLearnersTests.empty_search_is_no_filter
// issues two catalog-wide dbo.AdminListLearners reads (empty-string search, then
// NULL search) and asserts the totals are equal. AdminListEnrollmentsTests
// seeds/deletes its 12 "AdmPg032E" filler students one row at a time
// (individual auto-committed INSERTs in SeedFillerRows; bulk DELETEs in its
// stale cleanup), and AdminListLearnersTests itself seeds 12 "AdmPg032L"
// filler rows the same way; with class-level parallelism any of those rows can
// land between the two reads, so the totals differ by exactly the rows whose
// lifecycle straddled the gap. CI failure: Expected 25, Actual 24.
//
// Disabling assembly-level parallelization makes that interleaving impossible
// rather than unlikely, and covers every catalog-wide assertion in the project,
// not just the one that happened to fail. Same treatment as Catalog.Tests
// (f0ba9d6), which went from ~9s to ~14s.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
