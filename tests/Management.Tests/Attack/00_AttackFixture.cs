using System;
using System.Collections.Generic;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// Shared fixture helpers for the spec 059 attack suite (RED TEAM, Principle XIX).
///
/// Input contract (spec L4 + fixture rule): every parent -> child edge is wired
/// on BOTH sides:
///   (a) the child object is in the parent's Children collection, and
///   (b) child.ParentId = the parent's actual Id.
/// Roots keep ParentId = null (or their parent is absent from the flat input).
/// Input = the complete flat list of all nodes.
/// </summary>
public static class AttackFixture
{
    // Root organization: ParentId stays null.
    public static Organization Org(string name, params Organization[] children) =>
        new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Children = new List<Organization>(children)
        };

    // Child organization: wires both sides of the edge (ParentId set, child
    // registered in the parent's Children collection, order preserved).
    public static Organization Child(Organization parent, string name, params Organization[] grandchildren)
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

    /// <summary>Adopts an already-created child: sets ParentId and appends to Children.</summary>
    public static void Adopt(Organization parent, Organization child)
    {
        child.ParentId = parent.Id;
        parent.Children.Add(child);
    }

    /// <summary>
    /// Asserts the full spec 059 L6 output (count + per-name X/Y/Depth), in the
    /// expected pre-order (per root, roots in input order).
    /// </summary>
    public static void Expect(
        IList<(Organization Org, int X, int Y, int Depth)> actual,
        params (string name, int x, int y, int depth)[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var e = expected[i];
            Assert.True(actual[i].Org.Name == e.name,
                $"position {i}: expected '{e.name}' but found '{actual[i].Org.Name}'");
            Assert.Equal(e.x, actual[i].X);
            Assert.Equal(e.y, actual[i].Y);
            Assert.Equal(e.depth, actual[i].Depth);
        }
    }

    /// <summary>
    /// INV-1 (no shared (X,Y)) + INV-2 (no 240x50 rectangle overlap), all pairs.
    /// </summary>
    public static void AssertNoCollisionAndNoOverlap(
        IList<(Organization Org, int X, int Y, int Depth)> result)
    {
        for (var i = 0; i < result.Count; i++)
        {
            for (var j = i + 1; j < result.Count; j++)
            {
                var p = result[i];
                var q = result[j];
                Assert.False(p.X == q.X && p.Y == q.Y,
                    $"INV-1 violated: (X,Y) shared by '{p.Org.Name}' and '{q.Org.Name}' at ({p.X},{p.Y})");
                var intersects =
                    p.X < q.X + 240 && q.X < p.X + 240 &&
                    p.Y < q.Y + 50 && q.Y < p.Y + 50;
                Assert.False(
                    intersects,
                    $"INV-2 violated: rectangles of '{p.Org.Name}' [{p.X},{p.X + 240})x[{p.Y},{p.Y + 50}) and " +
                    $"'{q.Org.Name}' [{q.X},{q.X + 240})x[{q.Y},{q.Y + 50}) intersect");
            }
        }
    }

    /// <summary>INV-4: non-empty output has min X = 0.</summary>
    public static void AssertMinXIsZero(IList<(Organization Org, int X, int Y, int Depth)> result)
    {
        Assert.True(result.Count > 0, "expected non-empty output");
        Assert.Equal(0, result.Min(t => t.X));
    }

    /// <summary>INV-3: Y = Depth * 130 for every entry.</summary>
    public static void AssertYFollowsDepth(IList<(Organization Org, int X, int Y, int Depth)> result)
    {
        Assert.All(result, t => Assert.Equal(t.Depth * 130, t.Y));
    }
}
