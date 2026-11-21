using System.Collections.Generic;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 10 — mixed sibling widths in one parent (L2/L3 + INV-1/INV-2 hunting):
/// P -> [c1 (leaf, w 240), c2 (2 leaves, w 510), c3 (3 leaves, w 780)].
///
/// Spec-sequential values (frame start 0; all integers — no rounding edge):
///   c1 = 0 (leaf at frame start)
///   c2 frame at 240 + 30 = 270: c21 = 270, c22 = 540 -> c2 = (270 + 540)/2 = 405
///   c3 frame at 270 + 510 + 30 = 810: c31 = 810, c32 = 1080, c33 = 1350
///      -> c3 = (810 + 1350)/2 = 1080
///   P  = (0 + 1080)/2 = 540
///
/// Also checks L2 right-edge chaining (frame = previous right edge + 30) and
/// INV-4 (min X = 0) on a non-trivial 9-node shape.
/// </summary>
public class MixedWidths
{
    [Fact]
    public void mixed_sibling_widths_layout_is_collision_free_and_centered()
    {
        var c2 = AttackFixture.Org("c2");
        var c21 = AttackFixture.Child(c2, "c21");
        var c22 = AttackFixture.Child(c2, "c22");
        var c3 = AttackFixture.Org("c3");
        var c31 = AttackFixture.Child(c3, "c31");
        var c32 = AttackFixture.Child(c3, "c32");
        var c33 = AttackFixture.Child(c3, "c33");
        var p = AttackFixture.Org("P");
        var c1 = AttackFixture.Child(p, "c1");
        AttackFixture.Adopt(p, c2);
        AttackFixture.Adopt(p, c3);

        var orgs = new List<Organization> { p, c1, c2, c21, c22, c3, c31, c32, c33 };
        var result = new TreeLayoutService().ComputeLayout(orgs);

        // L6 pre-order: P, c1, c2, c21, c22, c3, c31, c32, c33.
        AttackFixture.Expect(result,
            ("P", 540, 0, 0),
            ("c1", 0, 130, 1),
            ("c2", 405, 130, 1),
            ("c21", 270, 260, 2),
            ("c22", 540, 260, 2),
            ("c3", 1080, 130, 1),
            ("c31", 810, 260, 2),
            ("c32", 1080, 260, 2),
            ("c33", 1350, 260, 2));

        // L3 centering.
        Assert.Equal((0 + 1080) / 2, result[0].X);  // P centered over c1 and c3
        Assert.Equal((270 + 540) / 2, result[2].X); // c2 centered over c21 and c22
        Assert.Equal((810 + 1350) / 2, result[5].X); // c3 centered over c31 and c33

        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertMinXIsZero(result);
        AttackFixture.AssertYFollowsDepth(result);
    }
}
