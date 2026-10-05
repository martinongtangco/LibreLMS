using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;
using Xunit;

namespace Management.Tests;

/// <summary>
/// Spec 058 US2 (FR-002): unit coverage for OrgSubtree (previously untested).
/// ADR 0010's subtree rule in one definition: SuperUser = null (no restriction),
/// OrgAdmin = own org + all descendants, None = fail-closed empty.
/// 3-level tree: R -> {A, B}; A -> {A1, A2}; B -> {B1}.
/// </summary>
public class OrgSubtreeTests
{
    private static readonly Guid R = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid A1 = Guid.NewGuid();
    private static readonly Guid A2 = Guid.NewGuid();
    private static readonly Guid B1 = Guid.NewGuid();

    /// <summary>Deterministic fake of the cross-module org lookup, driven by the fixed tree.</summary>
    private sealed class FakeOrgLookup : IOrganizationLookup
    {
        private static readonly Dictionary<Guid, Guid[]> Children = new()
        {
            [R] = [A, B],
            [A] = [A1, A2],
            [B] = [B1],
        };

        public Task<OrganizationSummary?> GetOrganizationAsync(Guid orgId) => Task.FromResult<OrganizationSummary?>(null);

        public Task<IList<Guid>> GetChildOrgIdsAsync(Guid parentId)
            => Task.FromResult<IList<Guid>>(Children.TryGetValue(parentId, out var kids) ? kids : Array.Empty<Guid>());

        public Task<IList<Guid>> GetAncestorOrgIdsAsync(Guid orgId)
        {
            // Walk up via the reverse of the Children map; includes the org itself.
            var parentOf = new Dictionary<Guid, Guid>();
            foreach (var (parent, kids) in Children)
                foreach (var kid in kids)
                    parentOf[kid] = parent;

            var ancestors = new List<Guid> { orgId };
            while (parentOf.TryGetValue(ancestors[^1], out var parent))
                ancestors.Add(parent);
            return Task.FromResult<IList<Guid>>(ancestors);
        }
    }

    private readonly FakeOrgLookup _lookup = new();

    [Fact]
    public async Task GetSubtreeOrgIdsAsync_superUser_returns_null_no_restriction()
    {
        var got = await OrgSubtree.GetSubtreeOrgIdsAsync(OrgScope.SuperUser, _lookup);

        Assert.Null(got);
    }

    [Fact]
    public async Task GetSubtreeOrgIdsAsync_none_returns_empty_failClosed()
    {
        var got = await OrgSubtree.GetSubtreeOrgIdsAsync(OrgScope.None, _lookup);

        Assert.NotNull(got);
        Assert.Empty(got!);
    }

    [Fact]
    public async Task GetSubtreeOrgIdsAsync_orgAdmin_returns_org_plus_all_descendants()
    {
        var got = await OrgSubtree.GetSubtreeOrgIdsAsync(OrgScope.ForOrgAdmin(A), _lookup);

        // A and its two children — NOT sibling B, NOT ancestor R.
        Assert.Equal(new HashSet<Guid> { A, A1, A2 }, got!);
    }

    [Fact]
    public async Task GetSubtreeOrgIdsAsync_orgAdmin_of_leaf_returns_just_that_leaf()
    {
        var got = await OrgSubtree.GetSubtreeOrgIdsAsync(OrgScope.ForOrgAdmin(B1), _lookup);

        Assert.Equal(new HashSet<Guid> { B1 }, got!);
    }

    [Fact]
    public async Task IsOrgInScopeAsync_matrix_self_descendant_in_sibling_ancestor_out()
    {
        var scope = OrgScope.ForOrgAdmin(A);

        var selfIn = await OrgSubtree.IsOrgInScopeAsync(scope, _lookup, A);
        var descendantIn = await OrgSubtree.IsOrgInScopeAsync(scope, _lookup, A1);
        var otherDescendantIn = await OrgSubtree.IsOrgInScopeAsync(scope, _lookup, A2);
        var siblingOut = await OrgSubtree.IsOrgInScopeAsync(scope, _lookup, B);
        var ancestorOut = await OrgSubtree.IsOrgInScopeAsync(scope, _lookup, R);
        var outsideOut = await OrgSubtree.IsOrgInScopeAsync(scope, _lookup, B1);

        Assert.True(selfIn, "own org is in scope");
        Assert.True(descendantIn, "direct child is in scope");
        Assert.True(otherDescendantIn, "other child is in scope");
        Assert.False(siblingOut, "sibling org is out of scope");
        Assert.False(ancestorOut, "ancestor is out of scope");
        Assert.False(outsideOut, "unrelated subtree is out of scope");
    }

    [Fact]
    public async Task IsOrgInScopeAsync_superUser_always_true()
    {
        Assert.True(await OrgSubtree.IsOrgInScopeAsync(OrgScope.SuperUser, _lookup, B1));
        Assert.True(await OrgSubtree.IsOrgInScopeAsync(OrgScope.SuperUser, _lookup, R));
    }

    [Fact]
    public async Task IsOrgInScopeAsync_none_always_false()
    {
        Assert.False(await OrgSubtree.IsOrgInScopeAsync(OrgScope.None, _lookup, A));
        Assert.False(await OrgSubtree.IsOrgInScopeAsync(OrgScope.None, _lookup, R));
    }
}
