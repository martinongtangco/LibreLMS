using System.Collections.Generic;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 02 — 3-level asymmetric tree (L2/L3 + INV-1/INV-2 hunting).
///
/// Shape: P -> [M, S]; M has 1 child (m); S has 3 children (s1, s2, s3).
/// Hostile: sibling subtree widths 240 vs 780 — parent centering must not let
/// the wide sibling's span bleed into the narrow one.
///
/// Spec-sequential values (frame start 0, all integers here — no rounding edge):
///   M: single child m at 0 -> M = (0 + 0)/2 = 0      (width 240)
///   S: frame at 240 + 30 = 270: s1 = 270, s2 = 540, s3 = 810
///      S = (270 + 810)/2 = 540                      (width 240*3 + 30*2 = 780)
///   P = (0 + 540)/2 = 270                          (width 240 + 780 + 30 = 1050)
///
/// Spec refs: L2, L3, L6, INV-1, INV-2, INV-3.
/// </summary>
public class Asymmetric3Level
{
    [Fact]
    public void asymmetric_widths_parent_centering_and_sibling_spacing()
    {
        var m = AttackFixture.Org("M");
        var mm = AttackFixture.Child(m, "m");
        var s = AttackFixture.Org("S");
        var s1 = AttackFixture.Child(s, "s1");
        var s2 = AttackFixture.Child(s, "s2");
        var s3 = AttackFixture.Child(s, "s3");
        var p = AttackFixture.Org("P");
        AttackFixture.Adopt(p, m);
        AttackFixture.Adopt(p, s);

        var orgs = new List<Organization> { p, m, mm, s, s1, s2, s3 };
        var result = new TreeLayoutService().ComputeLayout(orgs);

        // L6 pre-order: P, M, m, S, s1, s2, s3.
        AttackFixture.Expect(result,
            ("P", 270, 0, 0),
            ("M", 0, 130, 1),
            ("m", 0, 260, 2),
            ("S", 540, 130, 1),
            ("s1", 270, 260, 2),
            ("s2", 540, 260, 2),
            ("s3", 810, 260, 2));

        // L3 centering checks.
        Assert.Equal((0 + 540) / 2, result[0].X); // P centered over M and S
        Assert.Equal((270 + 810) / 2, result[3].X); // S centered over s1 and s3

        AttackFixture.AssertNoCollisionAndNoOverlap(result);
        AttackFixture.AssertMinXIsZero(result);
        AttackFixture.AssertYFollowsDepth(result);
    }
}
