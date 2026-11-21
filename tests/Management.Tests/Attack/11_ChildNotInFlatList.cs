using System;
using System.Collections.Generic;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 11 — abuse table row: "Children containing an org not in the flat
/// list | Excluded (L4: children must be members of the input)".
///
/// Input: R (root) with Children = [Q]; Q has ParentId = R.Id but Q is NOT a
/// member of the flat input list. Required: Q is excluded from the layout; R
/// renders as a lone leaf at (0, 0, 0). (Oracle suites do not cover this row.)
/// </summary>
public class ChildNotInFlatList
{
    [Fact]
    public void child_not_in_flat_list_is_excluded()
    {
        var r = AttackFixture.Org("R");
        var q = new Organization { Id = Guid.NewGuid(), Name = "Q", ParentId = r.Id };
        r.Children.Add(q); // Q is a child by Children, but absent from the flat list

        var orgs = new List<Organization> { r }; // Q deliberately not in the input
        var result = new TreeLayoutService().ComputeLayout(orgs);

        AttackFixture.Expect(result, ("R", 0, 0, 0));
        AttackFixture.AssertNoCollisionAndNoOverlap(result);
    }
}
