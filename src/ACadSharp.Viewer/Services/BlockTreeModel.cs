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
    /// Display name: block name, plus " ⚡" when the block is dynamic and
    /// " ×N" when the parent references it N times.
    /// </summary>
    public string DisplayName { get; }

    public bool IsDynamic => Block.IsDynamic;

    public List<BlockTreeNode> Children { get; } = new();

    public BlockTreeNode(BlockRecord block, string? suffix = null)
    {
        Block = block;

        string name = block.Name ?? "<unnamed>";
        if (block.IsDynamic)
        {
            name += " ⚡";
        }

        DisplayName = name + (suffix ?? string.Empty);
    }

    /// <summary>
    /// Copy for the filtered view: same block and display name, possibly
    /// pruned children (search filter).
    /// </summary>
    public BlockTreeNode(BlockRecord block, string displayName, List<BlockTreeNode> children)
    {
        Block = block;
        DisplayName = displayName;
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
            tree.Add(BuildNode(root, null, new List<BlockRecord> { root }, visited));
        }

        // Orphan/cycle fix: any block that is not reachable from a root (e.g. a
        // pure reference cycle) is appended as an extra root, marked with ⟳.
        foreach (BlockRecord block in blocks.Where(b => !visited.Contains(b)))
        {
            tree.Add(BuildNode(block, " ⟳", new List<BlockRecord> { block }, visited));
        }

        return tree;
    }

    private static BlockTreeNode BuildNode(
        BlockRecord block,
        string? suffix,
        List<BlockRecord> path,
        HashSet<BlockRecord> visited)
    {
        visited.Add(block);
        BlockTreeNode node = new(block, suffix);

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

            string? childSuffix = count > 1 ? $" ×{count}" : null;
            List<BlockRecord> childPath = new(path) { child };
            node.Children.Add(BuildNode(child, childSuffix, childPath, visited));
        }

        return node;
    }
}
