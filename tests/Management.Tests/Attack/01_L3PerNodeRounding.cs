using System;
using System.Collections.Generic;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 01 — spec 059 L3 (per-node rounding) / L6 (shift after rounding).
///
/// L3: "A node with children is centered over them: its X is the midpoint of its
/// first and last child's X (rounded to nearest integer, .5 away from zero)."
/// L6: "After layout, all X values are shifted so the minimum X is 0."
/// The spec's order of operations is: center + ROUND per node (L3), THEN shift
/// (L6). A parent therefore centers over its children's ALREADY-ROUNDED X values.
///
/// Fixture (hostile: two nested .5 midpoints, only one of which the SUT rounds
/// identically):
///   R -> [C, H] ; C -> [F, G] ; G -> [G1, G2] ; leaves: F, G1, G2, H
///
/// L2 widths:  G = 240 + 240 + 30 = 510
///             C = 240 (F) + 510 (G) + 30 = 780
///             R = 780 (C) + 240 (H) + 30 = 1050
///
/// Spec-sequential placement (frame start 0):
///   F  = 0          (leaf at frame start, L3)
///   G1 = 270, G2 = 540  (G frame at 240 + 30)
///   G  = (270 + 540)/2 = 405
///   C  = (0 + 405)/2 = 202.5  ->  L3 round .5 away from zero -> 203
///   H  = 810        (frame 780 + 30, leaf)
///   R  = (203 + 810)/2 = 506.5 -> L3 round .5 away from zero -> 507
/// L6: min X = 0 (F) -> shift by 0.
///
/// EXPECTED (spec): R = 507.
/// ACTUAL (SUT):    the SUT defers rounding to the end ("shift first, round once"
///                  per its own comment): R = (202.5 + 810)/2 = 506.25 -> 506.
/// The deferred computation feeds the UNROUNDED child X (202.5) into the parent's
/// midpoint, so the parent lands at 506 instead of the spec's 507.
///
/// Failure mode: wrong-output (off-by-one in R's X on shapes whose child
/// midpoint is fractional). Spec refs: L3, L6, FR-1.
/// </summary>
public class L3PerNodeRounding
{
    [Fact]
    public void R_is_centered_over_Cs_rounded_X()
    {
        // Fixture: R -> [C, H]; C -> [F, G]; G -> [G1, G2]; H, F, G1, G2 are leaves.
        var g = AttackFixture.Org("G");
        var g1 = AttackFixture.Child(g, "G1");
        var g2 = AttackFixture.Child(g, "G2");
        var c = AttackFixture.Org("C");
        var f = AttackFixture.Child(c, "F");
        AttackFixture.Adopt(c, g);
        var r = AttackFixture.Org("R");
        AttackFixture.Adopt(r, c);
        var h = AttackFixture.Child(r, "H");

        var orgs = new List<Organization> { r, c, f, g, g1, g2, h };
        var result = new TreeLayoutService().ComputeLayout(orgs);

        // L6 pre-order: R, C, F, G, G1, G2, H.
        // Spec-sequential L3 values (derived above):
        //   C = 203 (202.5 rounded, .5 away from zero)
        //   R = 507 ((203 + 810)/2 = 506.5 rounded, .5 away from zero)
        AttackFixture.Expect(result,
            ("R", 507, 0, 0),
            ("C", 203, 130, 1),
            ("F", 0, 260, 2),
            ("G", 405, 260, 2),
            ("G1", 270, 390, 3),
            ("G2", 540, 390, 3),
            ("H", 810, 130, 1));
    }
}
