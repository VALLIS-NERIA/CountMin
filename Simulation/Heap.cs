using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public sealed class FibonacciHeap <TKey, TValue> {
        class Node {
            public TKey Key;
            public TValue Value;
            public Node Parent;
            public List<Node> Children = new List<Node>();
            public bool Mark;

            public void AddChild(Node child) {
                child.Parent = this;
                Children.Add(child);
            }

            public override string ToString() { return string.Format("({0},{1})", Key, Value); }
        }

        readonly List<Node> root = new List<Node>();
        int count;
        Node min;

        public void Push(TKey key, TValue value) {
            Insert(new Node
            {
                Key = key,
                Value = value
            });
        }

        public KeyValuePair<TKey, TValue> Peek() {
            if (this.min == null)
                throw new InvalidOperationException();
            return new KeyValuePair<TKey, TValue>(this.min.Key, this.min.Value);
        }

        public KeyValuePair<TKey, TValue> Pop() {
            if (this.min == null)
                throw new InvalidOperationException();
            var min = ExtractMin();
            return new KeyValuePair<TKey, TValue>(min.Key, min.Value);
        }

        void Insert(Node node) {
            this.count++;
            this.root.Add(node);
            if (this.min == null) {
                this.min = node;
            }
            else if (Comparer<TKey>.Default.Compare(node.Key, this.min.Key) < 0) {
                this.min = node;
            }
        }

        Node ExtractMin() {
            var result = this.min;
            if (result == null)
                return null;
            foreach (var child in result.Children) {
                child.Parent = null;
                this.root.Add(child);
            }
            this.root.Remove(result);
            if (this.root.Count == 0) {
                this.min = null;
            }
            else {
                this.min = this.root[0];
                Consolidate();
            }
            this.count--;
            return result;
        }

        void Consolidate() {
            var a = new Node[UpperBound()];
            for (int i = 0; i < this.root.Count; i++) {
                var x = this.root[i];
                var d = x.Children.Count;
                while (true) {
                    var y = a[d];
                    if (y == null)
                        break;
                    if (Comparer<TKey>.Default.Compare(x.Key, y.Key) > 0) {
                        var t = x;
                        x = y;
                        y = t;
                    }
                    this.root.Remove(y);
                    i--;
                    x.AddChild(y);
                    y.Mark = false;
                    a[d] = null;
                    d++;
                }
                a[d] = x;
            }
            this.min = null;
            for (int i = 0; i < a.Length; i++) {
                var n = a[i];
                if (n == null)
                    continue;
                if (this.min == null) {
                    this.root.Clear();
                    this.min = n;
                }
                else {
                    if (Comparer<TKey>.Default.Compare(n.Key, this.min.Key) < 0) {
                        this.min = n;
                    }
                }
                this.root.Add(n);
            }
        }

        int UpperBound() { return (int) Math.Floor(Math.Log(this.count, (1.0 + Math.Sqrt(5)) / 2.0)) + 1; }
    }
}