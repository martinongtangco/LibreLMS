using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FsCheck.Xunit;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;
using Xunit;

namespace LibreLms.Modules.Management.Tests;

public class OrgChartLayoutTests
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

    private static void Check(
        IList<(Organization Org, int X, int Y, int Depth)> actual,
        params (string name, int x, int y, int depth)[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        foreach (var e in expected)
        {
            var entry = actual.FirstOrDefault(t => t.Org.Name == e.name);
            Assert.True(entry.Org != null, $"missing organization '{e.name}' in result");
            Assert.Equal(e.x, entry.X);
            Assert.Equal(e.y, entry.Y);
            Assert.Equal(e.depth, entry.Depth);
        }
    }

    private static List<Organization> Row1() =>
        new List<Organization> { Org("R") };

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

    private static List<Organization> Row5()
    {
        var r = Org("R");
        var a = Child(r, "A");
        var d = Child(r, "D");
        d.IsDeleted = true;
        return new List<Organization> { r, a, d };
    }

    private static List<Organization> Row7()
    {
        var r = Org("R");
        // Explicit ParentId = R.Id, but O is intentionally NOT in R.Children.
        var o = new Organization { Id = Guid.NewGuid(), Name = "O", ParentId = r.Id };
        return new List<Organization> { r, o };
    }

    private static List<Organization> Row8()
    {
        var r = Org("R");
        // Explicit construction: ParentId stays null (absent).
        var d = new Organization { Id = Guid.NewGuid(), Name = "D" };
        d.IsDeleted = true;
        return new List<Organization> { r, d };
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

    // ------------------------------------------------- acceptance (rows 1-10)

    [Fact]
    public void Acceptance_row1_single()
    {
        var result = new TreeLayoutService().ComputeLayout(Row1());
        Check(result, ("R", 0, 0, 0));
    }

    [Fact]
    public void Acceptance_row2_chain()
    {
        var result = new TreeLayoutService().ComputeLayout(Row2());
        Check(result, ("R", 0, 0, 0), ("A", 0, 130, 1), ("B", 0, 260, 2));
    }

    [Fact]
    public void Acceptance_row3_grid2x2()
    {
        var result = new TreeLayoutService().ComputeLayout(Row3());
        Check(result,
            ("R", 405, 0, 0),
            ("A", 135, 130, 1),
            ("B", 675, 130, 1),
            ("A1", 0, 260, 2),
            ("A2", 270, 260, 2),
            ("B1", 540, 260, 2),
            ("B2", 810, 260, 2));
    }

    [Fact]
    public void Acceptance_row4_forest()
    {
        var result = new TreeLayoutService().ComputeLayout(Row4());
        Check(result, ("R1", 0, 0, 0), ("R2", 270, 0, 0), ("C", 270, 130, 1));
    }

    [Fact]
    public void Acceptance_row5_deletedChild()
    {
        var result = new TreeLayoutService().ComputeLayout(Row5());
        Check(result, ("R", 0, 0, 0), ("A", 0, 130, 1));
    }

    [Fact]
    public void Acceptance_row6_empty()
    {
        var result = new TreeLayoutService().ComputeLayout(new List<Organization>());
        Check(result);
    }

    [Fact]
    public void Acceptance_row7_unreachableOrphan()
    {
        var result = new TreeLayoutService().ComputeLayout(Row7());
        Check(result, ("R", 0, 0, 0));
    }

    [Fact]
    public void Acceptance_row8_deletedRoot()
    {
        var result = new TreeLayoutService().ComputeLayout(Row8());
        Check(result, ("R", 0, 0, 0));
    }

    [Fact]
    public void Acceptance_row9_fanout1000()
    {
        var result = new TreeLayoutService().ComputeLayout(Row9());
        var expected = new List<(string name, int x, int y, int depth)>
        {
            ("R", 134858, 0, 0)
        };
        for (var i = 1; i <= 1000; i++)
            expected.Add(($"C{i}", (i - 1) * 270, 130, 1));
        Check(result, expected.ToArray());
    }

    [Fact]
    public void Acceptance_row10_cycle()
    {
        var sw = Stopwatch.StartNew();
        var result = new TreeLayoutService().ComputeLayout(Row10());
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"layout took {sw.Elapsed.TotalSeconds:F3}s (>= 5s)");
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Select(t => (t.X, t.Y)).Distinct().Count());
    }

    // ------------------------------------------------------- invariants/abuse

    [Fact]
    public void Invariant_yFollowsDepth()
    {
        foreach (var orgs in new[] { Row3(), Row9() })
        {
            var result = new TreeLayoutService().ComputeLayout(orgs);
            Assert.All(result, t => Assert.Equal(t.Depth * 130, t.Y));
        }
    }

    [Fact]
    public void Invariant_minXIsZero()
    {
        var fixtures = new[] { Row1(), Row2(), Row3(), Row4(), Row5(), Row7(), Row8(), Row9() };
        foreach (var orgs in fixtures)
        {
            var result = new TreeLayoutService().ComputeLayout(orgs);
            Assert.True(result.Count > 0, "fixture should be non-empty");
            Assert.Equal(0, result.Min(t => t.X));
        }
    }

    [Fact]
    public void Invariant_noRectangleOverlap()
    {
        var fixtures = new[] { Row2(), Row3(), Row4(), Row9() };
        foreach (var orgs in fixtures)
        {
            var result = new TreeLayoutService().ComputeLayout(orgs);
            for (var i = 0; i < result.Count; i++)
            {
                for (var j = i + 1; j < result.Count; j++)
                {
                    var p = result[i];
                    var q = result[j];
                    var intersects =
                        p.X < q.X + 240 && q.X < p.X + 240 &&
                        p.Y < q.Y + 50 && q.Y < p.Y + 50;
                    Assert.False(
                        intersects,
                        $"rectangles of '{p.Org.Name}' [{p.X},{p.X + 240})x[{p.Y},{p.Y + 50}) and " +
                        $"'{q.Org.Name}' [{q.X},{q.X + 240})x[{q.Y},{q.Y + 50}) intersect");
                }
            }
        }
    }

    [Fact]
    public void Abuse_500Chain()
    {
        var first = Org("N1");
        var orgs = new List<Organization> { first };
        var prev = first;
        for (var i = 2; i <= 500; i++)
        {
            var n = Child(prev, $"N{i}");
            orgs.Add(n);
            prev = n;
        }

        var result = new TreeLayoutService().ComputeLayout(orgs);

        Assert.Equal(500, result.Count);
        Assert.All(result, t => Assert.Equal(t.Depth * 130, t.Y));
        Assert.Equal(0, result.Min(t => t.X));
        Assert.Equal(result.Count, result.Select(t => (t.X, t.Y)).Distinct().Count());
    }

    [Fact]
    public void Abuse_duplicateId()
    {
        var r = Org("R");
        // Explicit construction: shares R's Guid; ParentId stays null (orphan).
        var o = new Organization { Id = r.Id, Name = "O" };

        var orgs = new List<Organization> { r, o };

        // No specific output asserted: the service must at least complete without throwing.
        new TreeLayoutService().ComputeLayout(orgs);
    }
}
