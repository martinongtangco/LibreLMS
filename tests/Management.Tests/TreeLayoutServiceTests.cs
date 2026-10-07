using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using Xunit;

namespace Management.Tests;

/// <summary>
/// Spec 058 US2 (FR-002): unit coverage for TreeLayoutService (previously untested).
/// Non-trivial shape per research R8: root + 2 children, one child with 2 children.
/// Y = depth × (NodeHeight 50 + LevelGap 80) = depth × 130; X positions distinct
/// within a row (no two nodes at the same depth may share an X — that is what
/// makes the SVG org chart legible); each parent centered over its children.
/// </summary>
public class TreeLayoutServiceTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid C1 = Guid.NewGuid();
    private static readonly Guid C2 = Guid.NewGuid();
    private static readonly Guid C1a = Guid.NewGuid();
    private static readonly Guid C1b = Guid.NewGuid();
    private static readonly Guid C1c = Guid.NewGuid(); // soft-deleted child of C1

    private static Organization Org(Guid id, Guid? parent, string name, bool deleted = false)
        => new() { Id = id, Name = name, ParentId = parent, IsDeleted = deleted };

    /// <summary>
    /// The service builds its tree from the Children navigation (the caller is expected to
    /// pass organizations with the navigation populated, as an EF Include would produce)
    /// while also requiring every node in the flat list. The fixtures below wire both.
    /// </summary>
    private static (Dictionary<Guid, (int X, int Y, int Depth)>, List<Organization>) Layout(List<Organization> flat)
    {
        var sut = new TreeLayoutService();
        var results = sut.ComputeLayout(flat);
        return (results.ToDictionary(r => r.Org.Id, r => (r.X, r.Y, r.Depth)), flat);
    }

    private static List<Organization> StandardShape()
    {
        var root = Org(Root, null, "Root");
        var c1 = Org(C1, Root, "C1");
        var c2 = Org(C2, Root, "C2");
        var c1a = Org(C1a, C1, "C1a");
        var c1b = Org(C1b, C1, "C1b");
        root.Children.Add(c1);
        root.Children.Add(c2);
        c1.Children.Add(c1a);
        c1.Children.Add(c1b);
        return new List<Organization> { root, c1, c2, c1a, c1b };
    }

    [Fact]
    public void ComputeLayout_emptyInput_returns_empty()
    {
        var (pos, _) = Layout(new List<Organization>());

        Assert.Empty(pos);
    }

    [Fact]
    public void ComputeLayout_depths_and_y_follow_depth_times_130()
    {
        var (pos, _) = Layout(StandardShape());

        Assert.Equal(5, pos.Count);
        Assert.Equal(0, pos[Root].Depth);
        Assert.Equal(1, pos[C1].Depth);
        Assert.Equal(1, pos[C2].Depth);
        Assert.Equal(2, pos[C1a].Depth);
        Assert.Equal(2, pos[C1b].Depth);

        Assert.Equal(0, pos[Root].Y);
        Assert.Equal(130, pos[C1].Y);
        Assert.Equal(130, pos[C2].Y);
        Assert.Equal(260, pos[C1a].Y);
        Assert.Equal(260, pos[C1b].Y);
    }

    [Fact]
    public void ComputeLayout_x_is_distinct_within_each_row()
    {
        var (pos, _) = Layout(StandardShape());

        // Group by depth; no two nodes in the same row may share an X.
        foreach (var row in pos.Values.GroupBy(v => v.Depth))
        {
            var xs = row.Select(v => v.X).ToList();
            Assert.True(xs.Count == xs.Distinct().Count(),
                $"row depth {row.Key} has duplicate X positions: {string.Join(", ", xs)}");
        }
    }

    [Fact]
    public void ComputeLayout_parent_centered_over_children()
    {
        var (pos, _) = Layout(StandardShape());

        // ±1 tolerance for the (int)Math.Round in CollectResults.
        Assert.True(Math.Abs(pos[C1].X - (pos[C1a].X + pos[C1b].X) / 2) <= 1,
            $"C1 X={pos[C1].X} is not centered over C1a X={pos[C1a].X} and C1b X={pos[C1b].X}");
        Assert.True(Math.Abs(pos[Root].X - (pos[C1].X + pos[C2].X) / 2) <= 1,
            $"Root X={pos[Root].X} is not centered over C1 X={pos[C1].X} and C2 X={pos[C2].X}");
    }

    [Fact]
    public void ComputeLayout_softDeleted_child_excluded()
    {
        var root = Org(Root, null, "Root");
        var c1 = Org(C1, Root, "C1");
        var c1a = Org(C1a, C1, "C1a");
        var c1b = Org(C1b, C1, "C1b");
        var c1c = Org(C1c, C1, "C1c (deleted)", deleted: true);
        root.Children.Add(c1);
        c1.Children.Add(c1a);
        c1.Children.Add(c1b);
        c1.Children.Add(c1c); // present in navigation AND flat list, but deleted
        var (pos, _) = Layout(new List<Organization> { root, c1, c1a, c1b, c1c });

        Assert.Equal(4, pos.Count);
        Assert.False(pos.ContainsKey(C1c), "soft-deleted child must not be laid out");
    }
}
