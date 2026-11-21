using System.Collections.Generic;
using System.Diagnostics;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 06 — self-cycle (S.Children includes S; S is also its own root,
/// ParentId null).
///
/// Required behavior (row 10 generalized: "each cyclic org rendered once; the
/// cycle entry is cut" + INV-7): terminates within 5 s, S rendered exactly once,
/// S's self-child entry is cut, no stack overflow. With the entry cut, S is a
/// lone leaf: (0, 0, 0).
/// </summary>
public class SelfCycle
{
    [Fact]
    public void self_cycle_renders_once_and_terminates()
    {
        var s = AttackFixture.Org("S");
        s.Children.Add(s);

        var orgs = new List<Organization> { s };
        var sw = Stopwatch.StartNew();
        var result = new TreeLayoutService().ComputeLayout(orgs);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"INV-7: self-cycle took {sw.Elapsed.TotalSeconds:F3}s (>= 5s)");
        Assert.Single(result);
        AttackFixture.Expect(result, ("S", 0, 0, 0));
    }
}
