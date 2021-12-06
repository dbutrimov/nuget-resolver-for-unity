using System.IO;
using System.Text;

namespace NuGetResolver.Editor {
  internal static class TreeNodePrinter {
    private static void Print<T>(this TreeNode<T> node, TextWriter writer, int depth) {
      if (depth > 0) {
        writer.Write(new string('\t', depth));
      }

      writer.WriteLine(node);
      if (node.Ignore) {
        return;
      }

      foreach (var child in node.Children) {
        child.Print(writer, depth + 1);
      }
    }

    public static void Print<T>(this TreeNode<T> node, TextWriter writer) {
      node.Print(writer, 0);
    }

    public static void Print<T>(this TreeNode<T> node, Stream stream) {
      using var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true);
      node.Print(writer);
    }

    public static void Print<T>(this TreeNode<T> node, string fileName) {
      using var stream = File.OpenWrite(fileName);
      node.Print(stream);
    }
  }
}
