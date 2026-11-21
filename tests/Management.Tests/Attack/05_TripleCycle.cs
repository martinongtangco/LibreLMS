using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 05 (re-pinned 2026-11-21 after the G1 spec decision) — 3-cycle
/// (A -> B -> C -> A), all three also roots (ParentId null, the most hostile
/// valid reading of L4: every member is a root).
///
/// G1 (root-identity priority, spec L4 amendment, user decision 2026-11-21):
/// every member is a root, so EVERY cycle edge is cut — the layout is three
/// root leaves in input order with L5 spacing (240 + 30), not a chain:
/// A = (0, 0, 0), B = (270, 0, 0), C = (540, 0, 0).
/// Required behavior (row 10 generalized to 3 + INV-6 + INV-7 + abuse row
/// "Cyclic Children"): terminates within 5 s; each cyclic org rendered exactly
/// once; no in-cycle child expanded; no coordinate collision; no stack overflow.
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
            ("B", 270, 0, 0),
            ("C", 540, 0, 0));

        Assert.Equal(3, result.Select(t => (t.X, t.Y)).Distinct().Count());
        AttackFixture.AssertNoCollisionAndNoOverlap(result);
    }
}
