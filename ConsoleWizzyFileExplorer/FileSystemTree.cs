using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ConsoleWizzyFileExplorer;

public sealed class FileSystemTree
{
    private const string WizTreePath = """C:\Program Files\WizTree\WizTree64.exe""";

    private readonly string _outputFileName;
    private string? _text;
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
        _text = File.ReadAllText(_outputFileName!);
        var textSpan = _text.AsSpan();
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
            var nameIndex = index + 1;
            if (!TryGetSpanOverLineAndAdvanceIndex(text, out var line, ref index)) { break; }

            var segmentIndex = line[1..].IndexOf('"');
            if (segmentIndex is -1) { continue; }
            var nameLength = segmentIndex;
            segmentIndex += 3;

            _ = TryGetSegment(line, out var size, ref segmentIndex);
            _ = TryGetSegment(line, out var allocated, ref segmentIndex);
            _ = TryGetSegment(line, out var modified, ref segmentIndex);
            _ = TryGetSegment(line, out var attributes, ref segmentIndex);

            var item = new FileSystemNode(
                nameIndex,
                nameLength,
                long.Parse(size),
                long.Parse(allocated),
                DateTimeOffset.Parse(modified),
                (FileSystemAttributes)uint.Parse(attributes)
            );

            _nodes[i++] = item;
        }

        return;

        static bool TryGetSpanOverLineAndAdvanceIndex(ReadOnlySpan<char> text, out ReadOnlySpan<char> line, ref int index)
        {
            if (!TryGetSpanToCharAndAdvanceIndex(text, '\r', out line, ref index)) { return false; }

            ++index;
            return true;
        }

        static bool TryGetSegment(ReadOnlySpan<char> text, out ReadOnlySpan<char> segment, ref int index) => TryGetSpanToCharAndAdvanceIndex(text, ',', out segment, ref index);

        static bool TryGetSpanToCharAndAdvanceIndex(ReadOnlySpan<char> text, char c, out ReadOnlySpan<char> segment, ref int index)
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

    private void BuildFileSystemNodeTree()
    {
        if (_nodes is null) { return; }

        var index = 0;
        RecursiveBuildFileSystemNodeTree(ref index, _nodes[0]);

        return;

        void RecursiveBuildFileSystemNodeTree(ref int index, FileSystemNode root)
        {
            var rootPath = GetText(root);

            while (++index < _nodes.Length)
            {
                var child = _nodes[index];
                child.Parent = root;

                var path = GetText(child);
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

    public ReadOnlySpan<char> GetText(FileSystemNode node) => _text.AsSpan()[node.NameIndex..(node.NameIndex + node.NameLength)];
}

public sealed record class FileSystemNode(
    int NameIndex,
    int NameLength,
    long Size,
    long Allocated,
    DateTimeOffset Modified,
    FileSystemAttributes Attributes
)
{
    public FileSystemNode? Parent { get; set; }
    public List<FileSystemNode>? Children { get; set; }
}

[Flags]
public enum FileSystemAttributes : uint
{
    None = 0,
    ReadOnly = 1,
    Hidden = 2,
    System = 4,
    Archive = 32,
    Compressed = 2048,
}
