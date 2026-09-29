using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// One node in the block hierarchy tree.
/// </summary>
public class BlockTreeNode
{
    public BlockRecord Block { get; }

    /// <summary>
    /// The block name (no markers; markers are shown as icons/badges in the
    /// tree template, so the name stays clean for search and the header).
    /// </summary>
    public string DisplayName { get; }

    public bool IsDynamic => Block.IsDynamic;

    /// <summary>
    /// How many times the parent block inserts this block (1 = single reference).
    /// </summary>
    public int ReferenceCount { get; }

    /// <summary>
    /// True when this node is a pure reference cycle root (appended as an extra root).
    /// </summary>
    public bool IsCycleRoot { get; }

    public bool ShowReferenceBadge => ReferenceCount > 1;

    public string ReferenceBadge => ReferenceCount > 1 ? $"×{ReferenceCount}" : string.Empty;

    public List<BlockTreeNode> Children { get; } = new();

    public BlockTreeNode(BlockRecord block, int referenceCount = 1, bool isCycleRoot = false)
    {
        Block = block;
        DisplayName = block.Name ?? "<unnamed>";
        ReferenceCount = referenceCount;
        IsCycleRoot = isCycleRoot;
    }

    /// <summary>
    /// Copy for the filtered view: same block and display name, possibly
    /// pruned children (search filter).
    /// </summary>
    public BlockTreeNode(BlockRecord block, int referenceCount, bool isCycleRoot, List<BlockTreeNode> children)
    {
        Block = block;
        DisplayName = block.Name ?? "<unnamed>";
        ReferenceCount = referenceCount;
        IsCycleRoot = isCycleRoot;
        Children = children;
    }
}

/// <summary>
/// Builds the block hierarchy tree of a document:
/// roots are the outermost blocks (blocks no other block inserts, with
/// *Model_Space / *Paper_Space first); children are the distinct blocks a
/// block inserts (deduplicated, with a ×N badge for repeated references).
/// </summary>
public static class BlockTreeModel
{
    public static List<BlockTreeNode> Build(CadDocument doc)
    {
        List<BlockRecord> blocks = doc.BlockRecords
            .Where(b => b is not null)
            .ToList();

        // For each referenced block, how many times is it inserted (and by whom).
        Dictionary<BlockRecord, int> referenceCounts = new();
        foreach (BlockRecord block in blocks)
        {
            foreach (Entity entity in block.Entities)
            {
                if (entity is Insert insert && insert.Block is not null)
                {
                    referenceCounts[insert.Block] = referenceCounts.GetValueOrDefault(insert.Block) + 1;
                }
            }
        }

        HashSet<BlockRecord> referenced = new(referenceCounts.Keys);

        // Roots: *Model_Space / *Paper_Space first, then every block that no
        // other block references.
        List<BlockRecord> roots = new();
        foreach (string spaceName in new[] { BlockRecord.ModelSpaceName, BlockRecord.PaperSpaceName })
        {
            BlockRecord? space = blocks.FirstOrDefault(b => b.Name == spaceName);
            if (space is not null)
            {
                roots.Add(space);
            }
        }

        roots.AddRange(blocks
            .Where(b => !referenced.Contains(b) && !roots.Contains(b))
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase));

        HashSet<BlockRecord> visited = new();
        List<BlockTreeNode> tree = new();
        foreach (BlockRecord root in roots)
        {
            tree.Add(BuildNode(root, 1, false, new List<BlockRecord> { root }, visited));
        }

        // Orphan/cycle fix: any block that is not reachable from a root (e.g. a
        // pure reference cycle) is appended as an extra root, flagged IsCycleRoot.
        foreach (BlockRecord block in blocks.Where(b => !visited.Contains(b)))
        {
            tree.Add(BuildNode(block, 1, true, new List<BlockRecord> { block }, visited));
        }

        return tree;
    }

    private static BlockTreeNode BuildNode(
        BlockRecord block,
        int referenceCount,
        bool isCycleRoot,
        List<BlockRecord> path,
        HashSet<BlockRecord> visited)
    {
        visited.Add(block);
        BlockTreeNode node = new(block, referenceCount, isCycleRoot);

        // Distinct blocks this block inserts, with their reference counts.
        Dictionary<BlockRecord, int> childCounts = new();
        foreach (Entity entity in block.Entities)
        {
            if (entity is Insert insert && insert.Block is not null)
            {
                BlockRecord target = insert.Block;
                childCounts[target] = childCounts.GetValueOrDefault(target) + 1;
            }
        }

        foreach ((BlockRecord child, int count) in childCounts.OrderBy(kv => kv.Key.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (path.Contains(child))
            {
                // Cycle guard: already on the current path, skip.
                continue;
            }

            List<BlockRecord> childPath = new(path) { child };
            node.Children.Add(BuildNode(child, count, false, childPath, visited));
        }

        return node;
    }
}
