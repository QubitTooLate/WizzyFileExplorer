// https://www.diskanalyzer.com/guide#cmdlinecsv

using ConsoleWizzyFileExplorer;
using System;

var tree = new FileSystemTree("""C:\""");
Console.WriteLine(tree.GetText(tree.GetDeepestNestedNode()).ToString());
//RecursiveWriteFileSystemTree(tree.Root, 0);
Console.ReadKey();

return;

void RecursiveWriteFileSystemTree(FileSystemNode item, int spaces)
{
    if (item.Children != null)
    {
        foreach (var child in item.Children)
        {
            Console.Write(new string(' ', spaces));
            Console.WriteLine(tree.GetText(child).ToString());
            RecursiveWriteFileSystemTree(child, spaces + 4);
        }
    }
}
