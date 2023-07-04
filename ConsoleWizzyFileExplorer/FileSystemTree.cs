using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ConsoleWizzyFileExplorer;

public sealed class FileSystemTree
{
    private const string WizTreePath = """C:\Program Files\WizTree\WizTree64.exe""";

    private readonly string _outputFileName;
    private FileSystemNode[]? _nodes;

    public FileSystemTree(string tree_of_directory_path)
    {
        _outputFileName = $"{Guid.NewGuid()}.csv";
        var stopwatch = Stopwatch.StartNew();
        LetWizTreeWriteFileSystemNodesToFile(tree_of_directory_path);
        Console.WriteLine($"WizTree time: {stopwatch.Elapsed}");
        stopwatch.Restart();
        ParseFileSystemNodesFromFile();
        File.Delete(_outputFileName);
        Console.WriteLine($"Parse and delete file: {stopwatch.Elapsed}");
        stopwatch.Restart();
        BuildFileSystemNodeTree();
        Console.WriteLine($"Build node tree: {stopwatch.Elapsed}");
        stopwatch.Restart();
        Console.WriteLine($"Nodes: {_nodes!.Length}");
    }

    public FileSystemNode Root => _nodes![0];

    public FileSystemNode GetDeepestNestedNode()
    {
        var deepestNestedNode = default(FileSystemNode);
        var deepestDepth = 0;
        foreach (var node in _nodes)
        {
            var depth = 0;
            var parent = node.Parent;
            while (parent is not null)
            {
                parent = parent.Parent;
                depth++;
            }

            if (depth > deepestDepth)
            {
                deepestNestedNode = node;
                deepestDepth = depth;
            }
        }

        return deepestNestedNode!;
    }

    private void LetWizTreeWriteFileSystemNodesToFile(string directory_path)
    {
        using var watcher = new FileSystemWatcher(Environment.CurrentDirectory, _outputFileName);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = WizTreePath,
            Arguments = $"""{directory_path} /export="{_outputFileName}" /admin=1""",
            WorkingDirectory = Environment.CurrentDirectory,
        });

        _ = watcher.WaitForChanged(WatcherChangeTypes.Changed);
        Console.ReadKey();
    }

    private ReadOnlySpan<char> ReadWizTreeFile(out int lineCount)
    {
        var text = File.ReadAllText(_outputFileName!);
        var textSpan = text.AsSpan();
        lineCount = CountReturnsInText(textSpan);
        return textSpan;

        static int CountReturnsInText(ReadOnlySpan<char> span)
        {
            var lineCount = 0;
            var index = 0;
            while (true)
            {
                var nextReturnIndex = span[index..].IndexOf('\r');
                if (nextReturnIndex is -1) { break; }
                index += nextReturnIndex + 2;
                lineCount++;
            }
            return lineCount;
        }
    }

    private void ParseFileSystemNodesFromFile()
    {
        var text = ReadWizTreeFile(out var lineCount);
        _nodes = new FileSystemNode[lineCount - 2];

        var i = 0;
        var index = 0;
        while (true)
        {
            if (!text.TryGetSpanOverLineAndAdvanceIndex(out var line, ref index)) { break; }
            if (!FileSystemNode.TryParse(line, out var node)) { continue; }
            _nodes[i++] = node!;
        }
    }

    private void BuildFileSystemNodeTree()
    {
        if (_nodes is null) { return; }

        var index = 0;
        RecursiveBuildFileSystemNodeTree(ref index, _nodes[0]);

        return;

        void RecursiveBuildFileSystemNodeTree(ref int index, FileSystemNode root)
        {
            var rootPath = root.FullName;

            while (++index < _nodes.Length)
            {
                var child = _nodes[index];
                child.Parent = root;

                var path = child.FullName;
                if (!path.StartsWith(rootPath))
                {
                    --index;
                    return;
                }

                root.Children ??= new List<FileSystemNode>();
                root.Children.Add(child);

                var isDirectory = path[^1] is '\\';
                if (isDirectory)
                {
                    RecursiveBuildFileSystemNodeTree(ref index, child);
                }
            }
        }
    }
}

public class FileSystemNode
{
    public static bool TryParse(ReadOnlySpan<char> text, out FileSystemNode? node)
    {
        node = null;
        var fullNameIndex = 1;

        var segmentIndex = text[1..].IndexOf('"');
        if (segmentIndex is -1) { return false; }
        segmentIndex += 3;

        var result =
            text.TryGetSpanToCharAndAdvanceIndex('"', out var fullNameSegment, ref fullNameIndex) &&
            text.TryGetSegment(out var sizeSegment, ref segmentIndex) &&
            long.TryParse(sizeSegment, out var size) &&
            text.TryGetSegment(out var allocatedSegment, ref segmentIndex) &&
            long.TryParse(allocatedSegment, out var allocated) &&
            text.TryGetSegment(out var modifiedSegment, ref segmentIndex) &&
            DateTimeOffset.TryParse(modifiedSegment, out var modified) &&
            text.TryGetSegment(out var attributesSegment, ref segmentIndex) &&
            ushort.TryParse(attributesSegment, out var attribute);
        
        if (!result) { return false; }

        var isDirectory = fullNameSegment[^1] is '\\';
        if (isDirectory)
        {
            node = new FileSystemDirectory(
                new string(fullNameSegment),
                size,
                allocated,
                modified,
                (FileSystemAttributes)attribute
            );
        }
        else
        {
            node = new FileSystemFile(
                new string(fullNameSegment),
                size,
                allocated,
                modified,
                (FileSystemAttributes)attribute
            );
        }

        return true;
    }

    public FileSystemNode(
        string full_name,
        long size,
        long allocated,
        DateTimeOffset last_modified_time,
        FileSystemAttributes attributes
    )
    {
        FullName = full_name;
        Size = size;
        Allocated = allocated;
        LastModifiedTime = last_modified_time;
        Attributes = attributes;
    }

    public string FullName { get; }
    
    public long Size { get; protected set; }
    
    public long Allocated { get; protected set; }
    
    public DateTimeOffset LastModifiedTime { get; protected set; }
    
    public FileSystemAttributes Attributes { get; protected set; }
    
    public FileSystemNode? Parent { get; set; }
    
    public List<FileSystemNode>? Children { get; set; }
}

public class FileSystemFile : FileSystemNode
{
    private FileInfo? _info;

    public FileSystemFile( string full_name, long size, long allocated, DateTimeOffset last_modified_time, FileSystemAttributes attributes) :
        base(full_name, size, allocated, last_modified_time, attributes)
    {

    }

    protected FileInfo Info => _info ??= new(FullName);
}

public class FileSystemDirectory : FileSystemNode
{
    private DirectoryInfo? _info;

    public FileSystemDirectory(string full_name, long size, long allocated, DateTimeOffset last_modified_time, FileSystemAttributes attributes) :
        base(full_name, size, allocated, last_modified_time, attributes)
    {

    }

    protected DirectoryInfo Info => _info ??= new(FullName);
}

[Flags]
public enum FileSystemAttributes : ushort
{
    None = 0,
    ReadOnly = 1,
    Hidden = 2,
    System = 4,
    Archive = 32,
    Compressed = 2048,
}

public static class ReadonlySpanExtensions
{
    public static bool TryGetSpanOverLineAndAdvanceIndex(this ReadOnlySpan<char> text, out ReadOnlySpan<char> line, ref int index)
    {
        if (!TryGetSpanToCharAndAdvanceIndex(text, '\r', out line, ref index)) { return false; }

        ++index;
        return true;
    }

    public static bool TryGetSegment(this ReadOnlySpan<char> text, out ReadOnlySpan<char> segment, ref int index) => TryGetSpanToCharAndAdvanceIndex(text, ',', out segment, ref index);

    public static bool TryGetSpanToCharAndAdvanceIndex(this ReadOnlySpan<char> text, char c, out ReadOnlySpan<char> segment, ref int index)
    {
        var endIndex = text[index..].IndexOf(c);
        if (endIndex is -1)
        {
            segment = default;
            return false;
        }

        segment = text[index..(index + endIndex)];
        index += endIndex + 1;
        return true;
    }
}
