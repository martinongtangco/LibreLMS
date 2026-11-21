using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 04 — 1000-node chain (abuse row: deep chain; INV-7 termination).
///
/// The SUT builds its tree recursively (BuildLayoutNode / SubtreeWidth /
/// PlaceSubtree / MinXOf / CollectResults all recurse to full depth). A 1000-deep
/// chain must not stack-overflow and must finish within the 5 s INV-7 budget.
///
/// Expected (L1/L3/L6, all X = 0 for a pure chain):
///   N1 = (0, 0, 0), N500 = (0, 499 * 130, 499), N1000 = (0, 999 * 130, 999)
///   INV-1: all (X,Y) distinct; INV-4: min X = 0; INV-3: Y = depth * 130.
/// </summary>
public class Chain1000
{
    [Fact]
    public void deep_chain_1000_terminates_without_stack_overflow()
    {
        var first = AttackFixture.Org("N1");
        var orgs = new List<Organization> { first };
        var prev = first;
        for (var i = 2; i <= 1000; i++)
        {
            var n = AttackFixture.Child(prev, $"N{i}");
            orgs.Add(n);
            prev = n;
        }

        var sw = Stopwatch.StartNew();
        var result = new TreeLayoutService().ComputeLayout(orgs);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"INV-7: 1000-node chain took {sw.Elapsed.TotalSeconds:F3}s (>= 5s)");
        Assert.Equal(1000, result.Count);

        var n1 = result.Single(t => t.Org.Name == "N1");
        var n500 = result.Single(t => t.Org.Name == "N500");
        var n1000 = result.Single(t => t.Org.Name == "N1000");
        Assert.Equal((0, 0, 0), (n1.X, n1.Y, n1.Depth));
        Assert.Equal((0, 499 * 130, 499), (n500.X, n500.Y, n500.Depth));
        Assert.Equal((0, 999 * 130, 999), (n1000.X, n1000.Y, n1000.Depth));

        Assert.All(result, t => Assert.Equal(0, t.X));
        Assert.Equal(1000, result.Select(t => (t.X, t.Y)).Distinct().Count());
        AttackFixture.AssertYFollowsDepth(result);
        AttackFixture.AssertMinXIsZero(result);
    }
}
