using System;
using System.Collections.Generic;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 09 — self-referential ParentId (O.ParentId = O.Id; O is a member of
/// the flat input but in nobody's Children collection).
///
/// Required behavior (L4 literal: roots = "ParentId is null OR does not match
/// any input org" — O.Id DOES match an input org, so O is NOT a root; L6/INV-6:
/// O is not reachable through any Children collection, so it is not rendered).
/// Only R may be rendered, at the single-root position.
/// </summary>
public class SelfParentId
{
    [Fact]
    public void self_parent_id_org_is_neither_root_nor_reachable()
    {
        var r = AttackFixture.Org("R");
        var o = new Organization { Id = Guid.NewGuid(), Name = "O" };
        o.ParentId = o.Id; // self-referential; O not in any Children collection

        var result = new TreeLayoutService().ComputeLayout(new List<Organization> { r, o });

        Assert.Single(result);
        AttackFixture.Expect(result, ("R", 0, 0, 0));
    }
}
