using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Management.Tests;

/// <summary>
/// Spec 058 US2 (FR-002): unit coverage for OrganizationLookup (previously untested).
/// EF InMemory ManagementDbContext, seeded org tree; unique database per class.
/// </summary>
public class OrganizationLookupTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid(); // deleted child of Root
    private static readonly Guid D = Guid.NewGuid(); // deleted child of A
    private static readonly Guid G = Guid.NewGuid(); // root of a 3-chain
    private static readonly Guid F = Guid.NewGuid(); // deleted middle of the chain
    private static readonly Guid E = Guid.NewGuid(); // child of F

    private readonly ManagementDbContext _db;
    private readonly OrganizationLookup _sut;

    public OrganizationLookupTests()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase($"OrgLookupTests_{Guid.NewGuid()}")
            .Options;
        _db = new ManagementDbContext(options);

        _db.Organizations.AddRange(
            new Organization { Id = Root, Name = "Root", ParentId = null },
            new Organization { Id = A, Name = "A", ParentId = Root },
            new Organization { Id = B, Name = "B", ParentId = A },
            new Organization { Id = C, Name = "C (deleted)", ParentId = Root, IsDeleted = true },
            new Organization { Id = D, Name = "D (deleted)", ParentId = A, IsDeleted = true },
            new Organization { Id = G, Name = "G", ParentId = null },
            new Organization { Id = F, Name = "F (deleted)", ParentId = G, IsDeleted = true },
            new Organization { Id = E, Name = "E", ParentId = F });
        _db.SaveChanges();

        _sut = new OrganizationLookup(_db);
    }

    [Fact]
    public async Task GetOrganizationAsync_found_returns_summary()
    {
        var got = await _sut.GetOrganizationAsync(A);

        Assert.NotNull(got);
        Assert.Equal(A, got!.Id);
        Assert.Equal("A", got.Name);
        Assert.Null(got.Description);
        Assert.Equal(Root, got.ParentId);
    }

    [Fact]
    public async Task GetOrganizationAsync_missing_returns_null()
    {
        var got = await _sut.GetOrganizationAsync(Guid.NewGuid());

        Assert.Null(got);
    }

    [Fact]
    public async Task GetOrganizationAsync_softDeleted_returns_null()
    {
        var got = await _sut.GetOrganizationAsync(C);

        Assert.Null(got);
    }

    [Fact]
    public async Task GetChildOrgIdsAsync_excludes_softDeleted_children()
    {
        var rootChildren = await _sut.GetChildOrgIdsAsync(Root);
        var aChildren = await _sut.GetChildOrgIdsAsync(A);

        // C is a deleted child of Root, D a deleted child of A — both excluded.
        Assert.Equal(new[] { A }, rootChildren);
        Assert.Equal(new[] { B }, aChildren);
    }

    [Fact]
    public async Task GetChildOrgIdsAsync_leaf_returns_empty()
    {
        var got = await _sut.GetChildOrgIdsAsync(B);

        Assert.Empty(got);
    }

    [Fact]
    public async Task GetAncestorOrgIdsAsync_includes_self_and_stops_at_root()
    {
        var got = await _sut.GetAncestorOrgIdsAsync(B);

        Assert.Equal(new[] { B, A, Root }, got);
    }

    [Fact]
    public async Task GetAncestorOrgIdsAsync_stops_at_deleted_parent()
    {
        // E -> F (deleted) -> G: the walk must stop at the deleted F, so G is not reached.
        var got = await _sut.GetAncestorOrgIdsAsync(E);

        Assert.Equal(new[] { E }, got);
    }

    [Fact]
    public async Task GetAncestorOrgIdsAsync_startingAtDeletedOrg_returns_empty()
    {
        var got = await _sut.GetAncestorOrgIdsAsync(F);

        Assert.Empty(got);
    }

    [Fact]
    public async Task GetAncestorOrgIdsAsync_missing_org_returns_empty()
    {
        var got = await _sut.GetAncestorOrgIdsAsync(Guid.NewGuid());

        Assert.Empty(got);
    }
}
