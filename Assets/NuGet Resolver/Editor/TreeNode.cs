using System.Collections.Generic;
using System.Linq;

namespace NuGetResolver.Editor {
  internal sealed class TreeNode<T> {
    public T Value { get; }
    public bool Ignore { get; }
    public IReadOnlyList<TreeNode<T>> Children { get; }

    public TreeNode(T value, bool ignore, IEnumerable<TreeNode<T>> children) {
      Value = value;
      Ignore = ignore;
      Children = children.ToList();
    }

    public override string ToString() {
      var name = ReferenceEquals(null, Value) ? "<Null>" : Value.ToString();
      return Ignore ? $"{name} (Ignored)" : name;
    }
  }
}
