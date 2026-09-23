using Xunit;

// Every class in this project reads and writes the same physical LearningLms
// Courses table (there is no per-test isolation), and xUnit runs test classes in
// parallel by default. That lets one class's setup/teardown mutate rows in the
// middle of another class's assertion.
//
// The failure this prevents: CourseLookupBulkTests
// .GetDistinctCategoriesAsync_MatchesDirectQuery_OrderedAndDistinct reads the
// catalog-wide service result and then a direct EF query, and compares the two.
// BrowseCoursesSortTests seeds 13 "AdmPg032C Even"/"AdmPg032C Odd" courses in
// InitializeAsync and deletes them in DisposeAsync; when that delete lands
// between the two reads, the first read still sees those categories and the
// second does not, so the collections differ.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
