using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElemType = System.Int64;

namespace Simulation {
    public class FSpaceSaving : ITopoSketch<Flow, ElemType> {
        private delegate uint HashFunc(object obj);

        private static Random rnd = new Random();

        public class Entry : IComparable<Entry> {
            public ElemType f;
            public ElemType e;

            public static Entry operator +(Entry left, (ElemType _f, ElemType _e) right) { return new Entry {e = left.e + right._e, f = left.f + right._f}; }

            public static implicit operator Entry((ElemType _f, ElemType _e) right) { return new Entry {e = right._e, f = right._f}; }

            public int CompareTo(Entry other) { return this.f.CompareTo(other.f); }
        }

        public class SwitchSketch:ISketch<ElemType> {
            public ElemType[] Alpha;
            public int[] Counter;
            private MinHeap<object, Entry> heap;
            private int h;
            private int m;

            private HashFunc hash;

            public SwitchSketch(int _w) {
                this.h = _w;
                this.m = _w;
                this.Alpha = new ElemType[h];
                this.Counter = new int[h];
                this.hash = hashFactory(rnd.Next());
                this.heap = new MinHeap<object, Entry>();
            }

            // not necessary in simulation
            // private Mutex mutex;
            private HashFunc hashFactory(int seed) { return o => (uint) ((uint) (o.GetHashCode() ^ seed) % this.h); }

            public void Update(object key, ElemType value) {
                var _u = this.heap.Count < this.m ? 0 : this.heap.Min.Value.f;
                int index = (int) hash(key);
                if (this.Counter[index] != 0) {
                    if (this.heap.ContainsKey(key)) {
                        this.heap[key].f += value;
                        return;
                    }
                }
                //this.Alpha[index] += value;

                if (this.Alpha[index] +value> _u) {
                    if (this.heap.Count == m) {
                        var min = this.heap.ExtractMin();
                        var kndex = this.hash(min.Key);
                        this.Counter[kndex] -= 1;
                        this.Alpha[kndex] = min.Value.f;
                    }
                    this.heap.Add(key, new Entry {f = this.Alpha[index]+value, e = this.Alpha[index]});
                    this.Counter[index] += 1;
                }
                else {
                    this.Alpha[index] += value;
                }
            }

            public ElemType Query(object key) {
                if (this.heap.ContainsKey(key)) {
                    var ret = this.heap[key].f /*- this.heap[key].e*/;
                    return ret > 0 ? ret : 0;
                }
                else {
                    return 0;
                }
            }

            public HashSet<T> GetKeys <T>() where T : class {
                var set = new HashSet<T>();
                foreach (var pair in this.heap) {
                    if (!set.Contains(pair.Key as T)) {
                        set.Add(pair.Key as T);
                    }
                }
                return set;
            }
        }


        private Dictionary<Switch, SwitchSketch> data;
        private int w;

        public FSpaceSaving(int _w) {
            this.w = _w;
            this.data = new Dictionary<Switch, SwitchSketch>();
        }

        public void Update(Flow flow, ElemType value) {
            var t = value;
            var packet = 30000000;
            while (t > 0) {
                _update(flow, t > packet ? packet : t);
                t -= packet;
            }
        }

        private void _update(Flow flow, ElemType value) {
            foreach (Switch sw in flow) {
                if (!data.ContainsKey(sw)) {
                    data.Add(sw, new SwitchSketch(w));
                }
                data[sw].Update(flow, value);
            }
        }

        public long Query(Flow flow) {
            List<ElemType> results = new List<ElemType>();
            foreach (var sw in flow) {
                var q = this.data[sw].Query(flow);
                if (q != 0) {
                    results.Add(q);
                }
            }
            if (results.Count == 0) {
                return 0;
            }
            return (ElemType)results.Average();
            // remove zeros
            results = (from d in results where d != 0 select d).ToList();
            // if only zeros return zero
            if (results.Count == 0) {
                return 0;
            }
            // find median
            results.Sort();
            var len = results.Count;
            if (len % 2 == 0) {
                return (results[len / 2] + results[len / 2 - 1]) / 2;
            }
            else {
                return results[(len - 1) / 2];
            }
        }

        public long this[Flow key] => Query(key);

        public IEnumerable<Flow> GetAllKeys() {
            var list = (IEnumerable<Flow>) new List<Flow>();
            foreach (var pair in this.data) {
                list = list.Concat(pair.Value.GetKeys<Flow>());
            }
            return new HashSet<Flow>(list);
        }
    }
}