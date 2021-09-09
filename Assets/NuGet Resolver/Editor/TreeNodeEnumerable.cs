using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace NuGetResolver.Editor {
  internal static class TreeNodeEnumerable {
    private class DepthTreeNodeEnumerable<T> : IEnumerable<TreeNode<T>> {
      private class DepthTreeNodeEnumerator : IEnumerator<TreeNode<T>> {
        private readonly TreeNode<T> _root;

        private int _disposeCallCount = -1;

        private TreeNode<T> _current;
        private int _currentIndex = -2;
        private IEnumerator<TreeNode<T>> _childEnumerator;

        public TreeNode<T> Current {
          get {
            ThrowIfDisposed();
            return _current;
          }
        }

        object IEnumerator.Current => Current;

        public DepthTreeNodeEnumerator(TreeNode<T> root) {
          _root = root;
        }

        private void ThrowIfDisposed() {
          if (_disposeCallCount >= 0) {
            throw new ObjectDisposedException(nameof(DepthTreeNodeEnumerator));
          }
        }

        public bool MoveNext() {
          ThrowIfDisposed();

          while (_currentIndex <= _root.Children.Count - 1) {
            if (_childEnumerator != null) {
              if (_childEnumerator.MoveNext()) {
                _current = _childEnumerator.Current;
                return true;
              }

              _childEnumerator.Dispose();
              _childEnumerator = null;
            }

            _currentIndex++;

            if (_currentIndex == -1) {
              _current = _root;
              return true;
            }

            if (_currentIndex >= 0 && _currentIndex <= _root.Children.Count - 1) {
              _childEnumerator = new DepthTreeNodeEnumerator(_root.Children[_currentIndex]);
            }
          }

          return false;
        }

        public void Reset() {
          ThrowIfDisposed();

          _current = default;
          _currentIndex = -2;
          _childEnumerator?.Dispose();
          _childEnumerator = null;
        }

        public void Dispose() {
          if (Interlocked.Increment(ref _disposeCallCount) > 0) {
            return;
          }

          _childEnumerator?.Dispose();
        }
      }


      private readonly TreeNode<T> _root;

      public DepthTreeNodeEnumerable(TreeNode<T> root) {
        _root = root;
      }

      public IEnumerator<TreeNode<T>> GetEnumerator() {
        return new DepthTreeNodeEnumerator(_root);
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }


    public static IEnumerable<TreeNode<T>> ToDepthEnumerable<T>(this TreeNode<T> node) {
      return new DepthTreeNodeEnumerable<T>(node);
    }
  }
}
