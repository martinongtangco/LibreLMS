using System.Collections.Generic;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 07 — an org that is BOTH a root (ParentId null, L4) and a child of
/// another org (in its Children collection), in both input orders.
///
/// Required behavior (L4 + L5 + L6 + INV-6 + INV-7): terminates; R is rendered
/// EXACTLY ONCE in both orders; rendered roots appear in input order; L5 root
/// spacing (270 = 240 + 30) holds when both are rendered as roots.
///
/// Order A: input [P, R] — P expands first and adopts R (R cut as a root):
///   P = (0, 0, 0), R = (0, 130, 1)  (L3: P centers over its single child R).
/// Order B: input [R, P] — R is the first root (lone leaf at 0); P's child R
/// is cut, so P is a lone leaf at the next frame:
///   R = (0, 0, 0), P = (270, 0, 0)  (L5: 240 + 30).
/// </summary>
public class RootAlsoChild
{
    [Fact]
    public void order_a_parent_first_renders_r_exactly_once_as_child()
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
            ("R", 0, 130, 1));
        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertMinXIsZero(result);
    }

    [Fact]
    public void order_b_root_first_renders_r_exactly_once_as_root_with_l5_spacing()
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
