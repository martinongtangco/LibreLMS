using System.Collections.Generic;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 07 (re-pinned 2026-11-21 after the G1 spec decision) — an org that is
/// BOTH a root (ParentId null, L4) and a child of another org (in its Children
/// collection), in both input orders.
///
/// G1 (root-identity priority, spec L4 amendment, user decision 2026-11-21):
/// R renders at depth 0 in BOTH orders; P's edge to R is cut, so P renders as
/// a lone leaf. Output is deterministic in input order:
///   Order A [P, R]: P = (0, 0, 0), R = (270, 0, 0)
///   Order B [R, P]: R = (0, 0, 0), P = (270, 0, 0)
/// (L5: roots left-to-right in input order, frame 240 + 30.)
/// </summary>
public class RootAlsoChild
{
    [Fact]
    public void order_a_parent_first_still_renders_r_as_root_not_child()
    {
        var r = AttackFixture.Org("R"); // ParentId null -> root per L4
        var p = AttackFixture.Org("P");
        p.Children.Add(r);             // but also P's child (no ParentId set on r)

        var orgs = new List<Organization> { p, r };
        var result = new TreeLayoutService().ComputeLayout(orgs);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result.Count(t => t.Org.Name == "R"));
        AttackFixture.Expect(result,
            ("P", 0, 0, 0),
            ("R", 270, 0, 0));          // G1: R at depth 0, NOT (0, 130, 1)
        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertMinXIsZero(result);
    }

    [Fact]
    public void order_b_root_first_renders_r_as_root_with_l5_spacing()
    {
        var r = AttackFixture.Org("R"); // ParentId null -> root per L4
        var p = AttackFixture.Org("P");
        p.Children.Add(r);

        var orgs = new List<Organization> { r, p };
        var result = new TreeLayoutService().ComputeLayout(orgs);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result.Count(t => t.Org.Name == "R"));
        AttackFixture.Expect(result,
            ("R", 0, 0, 0),
            ("P", 270, 0, 0));
        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertMinXIsZero(result);
    }
}
