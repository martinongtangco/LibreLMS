using System.Collections.Generic;
using System.Linq;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Tests.Attack;

/// <summary>
/// ATTACK 08 — every root is soft-deleted (row 8 + L4 generalized to a forest).
///
/// Required behavior: deleted roots are not rendered (row 8), so with no other
/// non-deleted roots the output is empty (row 6: empty output). No other org
/// may appear (INV-6).
/// </summary>
public class AllRootsDeleted
{
    [Fact]
    public void all_deleted_roots_produce_empty_output()
    {
        var d1 = new Organization { Id = System.Guid.NewGuid(), Name = "D1", IsDeleted = true };
        var d2 = new Organization { Id = System.Guid.NewGuid(), Name = "D2", IsDeleted = true };

        var result = new TreeLayoutService().ComputeLayout(new List<Organization> { d1, d2 });

        Assert.Empty(result);
    }
}
