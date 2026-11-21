using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Internal node used during tree layout computation.
/// Holds the organization reference and computed position data.
/// </summary>
internal class LayoutNode
{
    public Organization Org { get; set; } = null!;
    public List<LayoutNode> Children { get; set; } = new();
    public int Depth { get; set; }
    public double X { get; set; }
}

/// <summary>
/// Computes (X, Y) positions for organization nodes per the spec 059 layout
/// contract (L1-L6): deterministic, collision-free for arbitrary org shapes.
///
/// L1: Y = depth * 130 (node height 50 + level gap 80).
/// L2: a node occupies a 240-wide rectangle; the next sibling's subtree starts
///     at the previous subtree's right edge + 30 (subtree width = max(240,
///     sum(child widths) + 30 * (child count - 1))).
/// L3: a node with children is centered over them: X = midpoint of first and
///     last child's X (rounded to nearest integer, .5 away from zero); a leaf
///     sits at its frame's start.
/// L4: the tree is defined by the Children collections; roots are orgs whose
///     ParentId is null or does not match any input org; non-deleted children
///     in Children order. Deleted orgs (including deleted roots) are not
///     rendered. An org that is neither a root nor reachable through Children
///     is not rendered.
/// L5: multiple roots are laid out left-to-right in input order, each subtree
///     starting at the previous subtree's right edge + 30.
/// L6: all X values are shifted so the minimum X is 0; output order is
///     pre-order per root, roots in input order.
///
/// Cyclic input terminates: a child already visited on an earlier expansion is
/// cut, so each cyclic org renders once (spec 059 row 10 / INV-7). Duplicate
/// Ids are documented undefined behavior (abuse table) — the first occurrence
/// wins, preserving termination.
/// </summary>
public class TreeLayoutService
{
    private const int NodeWidth = 240;
    private const int NodeHeight = 50;
    private const int SiblingGap = 30;
    private const int LevelGap = 80;

    /// <summary>
    /// Build a layout tree from a flat list of organizations and compute positions.
    /// Returns a flat list of (Org, X, Y, Depth) in spec 059 L6 order.
    /// </summary>
    public IList<(Organization Org, int X, int Y, int Depth)> ComputeLayout(IList<Organization> orgs)
    {
        if (orgs.Count == 0)
            return Array.Empty<(Organization, int X, int Y, int Depth)>();

        // Duplicate Ids: documented undefined behavior (abuse table) — keep the
        // first occurrence so the layout terminates instead of throwing.
        var byId = orgs.GroupBy(o => o.Id)
                      .Select(g => g.First())
                      .ToDictionary(o => o.Id);

        // L4: roots = ParentId null or parent not in the input list; deleted
        // orgs (including deleted roots) are not rendered.
        var roots = orgs
            .Where(o => !o.IsDeleted && (!o.ParentId.HasValue || !byId.ContainsKey(o.ParentId.Value)))
            .ToList();

        // L4: Children are the authoritative tree; children must be non-deleted
        // members of the input list, in Children order. Visited set: a child
        // already expanded on an earlier path is cut, so cyclic input
        // terminates and each cyclic org renders once.
        var visited = new HashSet<Guid>();
        var treeRoots = new List<LayoutNode>();
        foreach (var r in roots)
        {
            if (visited.Add(r.Id))
                treeRoots.Add(BuildLayoutNode(r, byId, 0, visited));
        }

        foreach (var root in treeRoots)
            SubtreeWidth(root);

        // L5: place each root's frame after the previous subtree's right edge.
        var frameStart = 0.0;
        foreach (var root in treeRoots)
        {
            PlaceSubtree(root, frameStart);
            frameStart += SubtreeWidth(root) + SiblingGap;
        }

        // L6: shift so the minimum X is 0, then L3 rounding (nearest integer,
        // .5 away from zero) of (X - min) — shift first, round once.
        var min = treeRoots.Count > 0 ? MinX(treeRoots) : 0.0;

        var results = new List<(Organization Org, int X, int Y, int Depth)>();
        foreach (var root in treeRoots)
            CollectResults(root, min, results);

        return results;
    }

    private static double MinX(List<LayoutNode> roots)
    {
        var min = double.MaxValue;
        foreach (var root in roots)
            MinXOf(root, ref min);
        return min;
    }

    private static void MinXOf(LayoutNode node, ref double min)
    {
        if (node.X < min)
            min = node.X;
        foreach (var child in node.Children)
            MinXOf(child, ref min);
    }

    private static LayoutNode BuildLayoutNode(Organization org, Dictionary<Guid, Organization> byId, int depth, HashSet<Guid> visited)
    {
        var node = new LayoutNode
        {
            Org = org,
            Depth = depth
        };

        node.Children = org.Children
            .Where(c => !c.IsDeleted && byId.ContainsKey(c.Id) && visited.Add(c.Id))
            .Select(c => BuildLayoutNode(c, byId, depth + 1, visited))
            .ToList();

        return node;
    }

    /// <summary>L2: subtree width = max(240, sum(child widths) + 30 * (n - 1)).</summary>
    private static double SubtreeWidth(LayoutNode node)
    {
        if (node.Children.Count == 0)
            return NodeWidth;

        var childWidths = node.Children.Sum(SubtreeWidth);
        return Math.Max(NodeWidth, childWidths + SiblingGap * (node.Children.Count - 1));
    }

    /// <summary>
    /// L2/L3: place a subtree whose frame starts at x0: children left-to-right,
    /// each at the previous subtree's right edge + 30; a leaf sits at the
    /// frame's start; a node with children is centered over first/last child.
    /// </summary>
    private static void PlaceSubtree(LayoutNode node, double x0)
    {
        if (node.Children.Count == 0)
        {
            node.X = x0;
            return;
        }

        var cursor = x0;
        foreach (var child in node.Children)
        {
            PlaceSubtree(child, cursor);
            cursor += SubtreeWidth(child) + SiblingGap;
        }

        node.X = (node.Children[0].X + node.Children[^1].X) / 2;
    }

    /// <summary>L6 order (pre-order per root) + L1 (Y = depth * 130) + L3 rounding.</summary>
    private static void CollectResults(LayoutNode node, double min, List<(Organization Org, int X, int Y, int Depth)> results)
    {
        int x = (int)Math.Round(node.X - min, MidpointRounding.AwayFromZero);
        int y = node.Depth * (NodeHeight + LevelGap);

        results.Add((node.Org, x, y, node.Depth));

        foreach (var child in node.Children)
            CollectResults(child, min, results);
    }
}
