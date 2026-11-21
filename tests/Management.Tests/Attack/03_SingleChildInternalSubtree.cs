using System.Collections.Generic;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 03 — a node whose single child is an internal subtree (width 510 > 240),
/// plus the exactly-240 chain edge (parent and child share X, differ in Y).
///
/// Shape: P -> M; M -> [m1, m2].
///
/// Spec values:
///   m1 = 0, m2 = 270 (M frame at 0; leaf at frame start)
///   M  = (0 + 270)/2 = 135        (width 510)
///   P  = (135 + 135)/2 = 135      (single child: centers over itself)
///
/// P's 240-wide rectangle [135, 375) fits inside its subtree span [0, 510);
/// the chain edge P/M share X = 135 but Y differs by 130 (height 50 -> 80 gap),
/// so no rectangle overlap. INV-1 distinguishes them by Y.
///
/// Spec refs: L2, L3, L1, INV-1, INV-2.
/// </summary>
public class SingleChildInternalSubtree
{
    [Fact]
    public void parent_over_internal_subtree_fits_and_chain_edge_does_not_overlap()
    {
        var m = AttackFixture.Org("M");
        var m1 = AttackFixture.Child(m, "m1");
        var m2 = AttackFixture.Child(m, "m2");
        var p = AttackFixture.Org("P");
        AttackFixture.Adopt(p, m);

        var orgs = new List<Organization> { p, m, m1, m2 };
        var result = new TreeLayoutService().ComputeLayout(orgs);

        // L6 pre-order: P, M, m1, m2.
        AttackFixture.Expect(result,
            ("P", 135, 0, 0),
            ("M", 135, 130, 1),
            ("m1", 0, 260, 2),
            ("m2", 270, 260, 2));

        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertMinXIsZero(result);
        AttackFixture.AssertYFollowsDepth(result);
    }
}
