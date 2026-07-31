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
    public double PrelimX { get; set; }       /* preliminary x-coordinate */
    public double ModifierX { get; set; }     /* adjustment for even-depth nodes */
    public double ChangeX { get; set; }       /* adjustment for odd-depth nodes */
    public double ThreadX { get; set; }       /* left-thread x-coordinate */
    public LayoutNode? Parent { get; set; }
    public LayoutNode? LeftMost { get; set; } /* leftmost descendant */
    public LayoutNode? RightMost { get; set; } /* rightmost descendant */
    public bool IsLeft { get; set; }
    public bool IsRight { get; set; }
    public int Layer { get; set; }
}

/// <summary>
/// Computes (X, Y) positions for organization nodes using a simplified
/// Reingold-Tilford (tidy tree) layout algorithm.
/// 
/// The algorithm assigns positions top-down: root at top, children below,
 /// with siblings spaced evenly and subtrees centered.
/// </summary>
public class TreeLayoutService
{
    private const int NodeWidth = 240;
    private const int NodeHeight = 50;
    private const int SiblingGap = 30;
    private const int LevelGap = 80;

    /// <summary>
    /// Build a layout tree from a flat list of organizations and compute positions.
    /// Returns a flat list of LayoutResult with assigned (X, Y) coordinates.
    /// </summary>
    public IList<(Organization Org, int X, int Y, int Depth)> ComputeLayout(IList<Organization> orgs)
    {
        if (orgs.Count == 0)
            return Array.Empty<(Organization, int, int, int)>();

        // Build tree from flat list
        var byId = orgs.ToDictionary(o => o.Id);
        var roots = orgs.Where(o => !o.ParentId.HasValue || !byId.ContainsKey(o.ParentId.Value)).ToList();
        
        var treeNodes = roots.Select(o => BuildLayoutNode(o, byId, 0)).ToList();

        // Run the layout algorithm on each root
        var results = new List<(Organization, int, int, int)>();

        foreach (var root in treeNodes)
        {
            FirstWalk(root, 0);
            SecondWalk(root, 0, 0);
            CollectResults(root, results);
        }

        return results;
    }

    private LayoutNode BuildLayoutNode(Organization org, Dictionary<Guid, Organization> byId, int depth)
    {
        var node = new LayoutNode
        {
            Org = org,
            Depth = depth,
            IsLeft = true,  /* first child is leftmost */
            IsRight = true  /* last child is rightmost */
        };

        var children = org.Children
            .Where(c => !c.IsDeleted && byId.ContainsKey(c.Id))
            .Select(c => BuildLayoutNode(c, byId, depth + 1))
            .ToList();

        node.Children = children;

        // Update left/right flags
        if (children.Count > 0)
        {
            children[0].IsLeft = true;
            children[^1].IsLeft = false;
            children[^1].IsRight = true;
            children[0].IsRight = false;
        }

        return node;
    }

    /// <summary>
    /// First walk: assign preliminary positions and compute subtree widths.
    /// </summary>
    private void FirstWalk(LayoutNode node, int depth)
    {
        node.Layer = depth;
        node.LeftMost = node;
        node.RightMost = node;

        if (node.Children.Count == 0)
        {
            node.PrelimX = node.Parent != null ? node.Parent.PrelimX + NodeWidth + SiblingGap : 0;
            return;
        }

        var prevChild = node.Children[0];
        foreach (var child in node.Children.Skip(1))
        {
            FirstWalk(child, depth + 1);
            child.PrelimX = prevChild.PrelimX + NodeWidth + SiblingGap;
            prevChild = child;
        }

        /* Center parent over children */
        var first = node.Children[0];
        var last = node.Children[^1];
        node.PrelimX = (first.PrelimX + last.PrelimX) / 2 + NodeWidth / 2 - NodeWidth / 2;
    }

    /// <summary>
    /// Second walk: apply modifiers to get final positions.
    /// </summary>
    private void SecondWalk(LayoutNode node, int depth, double modifier)
    {
        node.PrelimX += modifier;

        foreach (var child in node.Children)
        {
            SecondWalk(child, depth + 1, modifier);
        }
    }

    /// <summary>
    /// Collect layout results from the tree into a flat list.
    /// </summary>
    private void CollectResults(LayoutNode node, List<(Organization, int, int, int)> results)
    {
        int x = (int)Math.Round(node.PrelimX);
        int y = node.Depth * (NodeHeight + LevelGap);

        results.Add((node.Org, x, y, node.Depth));

        foreach (var child in node.Children)
        {
            CollectResults(child, results);
        }
    }
}
