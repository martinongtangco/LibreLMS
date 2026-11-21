using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using Xunit;

namespace LibreLms.Modules.Management.Tests.Holdout;

public class OrgChartLayoutHoldoutTests
{
    // ---------------------------------------------------------------- helpers
    //
    // Input contract (spec L4): the tree is defined by the Children collections,
    // and every node of the tree appears in the input list. Roots = orgs whose
    // ParentId is null or whose ParentId does not match any input org.
    // Non-deleted children appear in Children order.
    // For every parent -> child edge the fixture therefore wires BOTH sides:
    //   (a) the child object is in the parent's Children collection, and
    //   (b) child.ParentId = the parent's actual Id.

    // Root organization: ParentId stays null.
    private static Organization Org(string name, params Organization[] children) =>
        new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Children = new List<Organization>(children)
        };

    // Child organization: wires both sides of the edge — ParentId is set to the
    // parent's actual Id and the child is registered in the parent's Children
    // collection (order preserved).
    private static Organization Child(Organization parent, string name, params Organization[] grandchildren)
    {
        var child = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentId = parent.Id,
            Children = new List<Organization>(grandchildren)
        };
        parent.Children.Add(child);
        return child;
    }

    private static List<Organization> Row2()
    {
        var r = Org("R");
        var a = Child(r, "A");
        var b = Child(a, "B");
        return new List<Organization> { r, a, b };
    }

    private static List<Organization> Row3()
    {
        var r = Org("R");
        var a = Child(r, "A");
        var b = Child(r, "B");
        var a1 = Child(a, "A1");
        var a2 = Child(a, "A2");
        var b1 = Child(b, "B1");
        var b2 = Child(b, "B2");
        return new List<Organization> { r, a, b, a1, a2, b1, b2 };
    }

    private static List<Organization> Row4()
    {
        var r1 = Org("R1");
        var r2 = Org("R2");
        var c = Child(r2, "C");
        return new List<Organization> { r1, r2, c };
    }

    private static List<Organization> Row7()
    {
        var r = Org("R");
        // Explicit ParentId = R.Id, but O is intentionally NOT in R.Children.
        var o = new Organization { Id = Guid.NewGuid(), Name = "O", ParentId = r.Id };
        return new List<Organization> { r, o };
    }

    private static List<Organization> Row9()
    {
        var r = Org("R");
        var orgs = new List<Organization> { r };
        for (var i = 1; i <= 1000; i++)
            orgs.Add(Child(r, $"C{i}"));
        return orgs;
    }

    private static List<Organization> Row10()
    {
        var a = Org("A");
        var b = Org("B");
        a.Children.Add(b);
        b.Children.Add(a);
        return new List<Organization> { a, b };
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public void Holdout_row2_chain_coordinates()
    {
        var result = new TreeLayoutService().ComputeLayout(Row2());
        Assert.Equal(3, result.Count);

        // Entries are selected by Name (output order by Name); every field of
        // every entry is asserted individually (decomposition distinct from the
        // visible suite's whole-list check).
        var r = result.Single(t => t.Org.Name == "R"); // entry[0]
        var a = result.Single(t => t.Org.Name == "A"); // entry[1]
        var b = result.Single(t => t.Org.Name == "B"); // entry[2]

        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
        Assert.Equal(0, r.Depth);
        Assert.Equal(0, a.X);
        Assert.Equal(130, a.Y);
        Assert.Equal(1, a.Depth);
        Assert.Equal(0, b.X);
        Assert.Equal(260, b.Y);
        Assert.Equal(2, b.Depth);
    }

    [Fact]
    public void Holdout_row3_grid2x2_noSharedCoordinates()
    {
        var result = new TreeLayoutService().ComputeLayout(Row3());
        Assert.Equal(7, result.Count);

        // ALL C(7,2) = 21 pairs checked explicitly in a loop:
        // (a) pairwise distinct (X,Y) points, and
        // (b) pairwise non-intersecting rectangles [x, x+240) x [y, y+50).
        var pairCount = 0;
        for (var i = 0; i < result.Count; i++)
        {
            for (var j = i + 1; j < result.Count; j++)
            {
                pairCount++;
                var p = result[i];
                var q = result[j];
                var sharedPoint = p.X == q.X && p.Y == q.Y;
                Assert.False(sharedPoint, $"(X,Y) shared by '{p.Org.Name}' and '{q.Org.Name}' at ({p.X},{p.Y})");
                var intersects =
                    p.X < q.X + 240 && q.X < p.X + 240 &&
                    p.Y < q.Y + 50 && q.Y < p.Y + 50;
                Assert.False(
                    intersects,
                    $"rectangles of '{p.Org.Name}' [{p.X},{p.X + 240})x[{p.Y},{p.Y + 50}) and " +
                    $"'{q.Org.Name}' [{q.X},{q.X + 240})x[{q.Y},{q.Y + 50}) intersect");
            }
        }
        Assert.Equal(21, pairCount);
    }

    [Fact]
    public void Holdout_row4_forest_rootSeparation()
    {
        var result = new TreeLayoutService().ComputeLayout(Row4());
        var r1 = result.Single(t => t.Org.Name == "R1");
        var r2 = result.Single(t => t.Org.Name == "R2");
        Assert.Equal(270, r2.X - r1.X);
    }

    [Fact]
    public void Holdout_invariant5_deterministic()
    {
        var orgs = Row3();
        var service = new TreeLayoutService();
        var first = service.ComputeLayout(orgs);
        var second = service.ComputeLayout(orgs);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            // element-identical: same Name order, same X, Y, Depth at each index
            Assert.Equal(first[i].Org.Name, second[i].Org.Name);
            Assert.Equal(first[i].X, second[i].X);
            Assert.Equal(first[i].Y, second[i].Y);
            Assert.Equal(first[i].Depth, second[i].Depth);
        }
    }

    [Fact]
    public void Holdout_invariant6_completeness()
    {
        // 2x2 fixture: the set of rendered names must be exactly the 7 fixture names.
        var grid = Row3();
        var expectedGrid = new[] { "R", "A", "B", "A1", "A2", "B1", "B2" };
        var renderedGrid = new TreeLayoutService()
            .ComputeLayout(grid)
            .Select(t => t.Org.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(expectedGrid.OrderBy(n => n).ToArray(), renderedGrid);

        // Row-7 fixture (unreachable orphan): only R may be rendered.
        var orphan = Row7();
        var renderedOrphan = new TreeLayoutService()
            .ComputeLayout(orphan)
            .Select(t => t.Org.Name)
            .ToArray();
        Assert.Equal(new[] { "R" }, renderedOrphan);
    }

    [Fact]
    public void Holdout_abuse_cycle_bounded()
    {
        // A <-> B cycle. The call is bounded to a 5 s budget; it must COMPLETE
        // (pre-implementation the service recurses without a cycle guard and the
        // process crashes with a stack overflow instead of returning).
        var task = Task.Run(() => new TreeLayoutService().ComputeLayout(Row10()));
        var completed = task.Wait(TimeSpan.FromSeconds(5));
        Assert.True(completed, "ComputeLayout did not complete within the 5s budget");

        var result = task.Result;
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Select(t => (t.X, t.Y)).Distinct().Count());
    }

    [Fact]
    public void Holdout_abuse_fanout1000_extremes()
    {
        var result = new TreeLayoutService().ComputeLayout(Row9());
        var c1 = result.Single(t => t.Org.Name == "C1");
        var c500 = result.Single(t => t.Org.Name == "C500");
        var c1000 = result.Single(t => t.Org.Name == "C1000");
        var r = result.Single(t => t.Org.Name == "R");

        Assert.Equal(0, c1.X);
        Assert.Equal(999 * 270, c1000.X); // 269730 = 2 * 134865 (spec-corrected root midpoint)
        Assert.Equal(499 * 270, c500.X);  // 134730
        Assert.Equal(134865, r.X);
    }
}
