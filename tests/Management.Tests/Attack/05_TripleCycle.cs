using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 05 — 3-cycle (A -> B -> C -> A), all three also roots (ParentId null,
/// the most hostile valid reading of L4: every member is a root).
///
/// Required behavior (row 10 generalized to 3 + INV-6 + INV-7 + abuse row
/// "Cyclic Children"): terminates within 5 s; each cyclic org rendered exactly
/// once; the cycle entry (C's child A) is cut so the in-cycle child is not
/// expanded; no coordinate collision; no stack overflow.
///
/// With the cycle entry cut, A-B-C is a plain chain, so the L1/L3/L6 layout is
/// exactly: A = (0,0,0), B = (0,130,1), C = (0,260,2).
/// </summary>
public class TripleCycle
{
    [Fact]
    public void three_node_cycle_renders_each_org_once_and_terminates()
    {
        var a = AttackFixture.Org("A");
        var b = AttackFixture.Org("B");
        var c = AttackFixture.Org("C");
        a.Children.Add(b);
        b.Children.Add(c);
        c.Children.Add(a);

        var orgs = new List<Organization> { a, b, c };
        var sw = Stopwatch.StartNew();
        var result = new TreeLayoutService().ComputeLayout(orgs);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"INV-7: 3-cycle took {sw.Elapsed.TotalSeconds:F3}s (>= 5s)");
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "A", "B", "C" }, result.Select(t => t.Org.Name).ToArray());

        AttackFixture.Expect(result,
            ("A", 0, 0, 0),
            ("B", 0, 130, 1),
            ("C", 0, 260, 2));

        Assert.Equal(3, result.Select(t => (t.X, t.Y)).Distinct().Count());
        AttackFixture.AssertNoCollisionAndNoOverlap(result);
    }
}
