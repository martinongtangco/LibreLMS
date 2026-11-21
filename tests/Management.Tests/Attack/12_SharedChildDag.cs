using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 12 — shared-child DAG: a non-deleted org S that appears in the
/// Children collections of TWO different roots (S.ParentId = P1.Id; P2 is the
/// non-owning parent).
///
/// Spec-mandatory behavior only (INV-6 + INV-7 + INV-1/INV-2): terminates
/// within 5 s, S rendered EXACTLY ONCE, no coordinate collision, no rectangle
/// overlap.
///
/// spec-gap: the spec (L4: "the tree is defined by the Children collections")
/// presumes a tree; for a DAG it does NOT define how the non-owning parent's
/// width/centering must treat the cut child. The implementation lets the
/// first-encountered parent own the child and removes it from the other
/// parent's Children entirely (so the other parent's width excludes it). No
/// assertion here encodes that preference.
/// </summary>
public class SharedChildDag
{
    [Fact]
    public void shared_child_renders_exactly_once_and_terminates()
    {
        var p1 = AttackFixture.Org("P1");
        var p2 = AttackFixture.Org("P2");
        var s = new Organization { Id = Guid.NewGuid(), Name = "S", ParentId = p1.Id };
        p1.Children.Add(s);
        p2.Children.Add(s); // same object, two parents

        var orgs = new List<Organization> { p1, p2, s };
        var sw = Stopwatch.StartNew();
        var result = new TreeLayoutService().ComputeLayout(orgs);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"INV-7: shared-child DAG took {sw.Elapsed.TotalSeconds:F3}s (>= 5s)");
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result.Count(t => t.Org.Name == "S"));
        Assert.Equal(1, result.Count(t => t.Org.Name == "P1"));
        Assert.Equal(1, result.Count(t => t.Org.Name == "P2"));

        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertYFollowsDepth(result);
        AttackFixture.AssertMinXIsZero(result);
    }
}
