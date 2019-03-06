using System;
using System.Collections.Generic;
using System.Linq;
using ElemType = System.Int64;

namespace Simulation.Sketches {
    public class CountSketch : IReversibleSketch<Flow, ElemType> {
        private delegate uint HashFunc(object obj);

        private delegate int SHashFunc(object obj);

        public class CSLine {
            public ElemType[] Count;
            private int w;

            private HashFunc hash;

            private SHashFunc sHash;

            public CSLine(int _w) {
                this.w = _w;
                this.Count = new ElemType[this.w];
                this.hash = this.hashFactory(rnd.Next());
                this.sHash = this.sHashFactory(rnd.Next());
            }

            public ElemType this[object key] => this.Query(key);

            // not necessary in simulation
            // private Mutex mutex;
            private HashFunc hashFactory(int seed) { return o => (uint) ((uint) (o.GetHashCode() ^ seed) % this.w); }

            private SHashFunc sHashFactory(int seed) {
                return delegate(object o)
                {
                    var h = o.GetHashCode();
                    var m = 0;
                    for (int i = 0; i < 32; i++) {
                        var c = h >> i & 1;
                        m += c;
                    }
                    m = m % 2;
                    return m == 0 ? -1 : 1;
                };
            }

            public int Update(object key, ElemType value) {
                int index = (int) this.hash(key);
                int sign = this.sHash(key);
                this.Count[index] += sign * value;
                return index;
            }

            public ElemType Query(object key) {
                int index = (int) this.hash(key);
                int sign = this.sHash(key);
                return this.Count[index] * sign;
            }
        }

        public class SwitchSketch : ISketch<ElemType> {
            private CSLine[] stat;
            private int w, d;

            private MinHeap<object, ElemType> heap;

            public SwitchSketch(int _w, int _d) {
                this.w = _w;
                this.d = _d;
                this.stat = new CSLine[this.d];
                this.heap = new MinHeap<object, long>();
                for (int i = 0; i < this.d; i++) {
                    this.stat[i] = (new CSLine(this.w));
                }
            }

            public void Update(object key, ElemType value) {
                foreach (CSLine cmLine in this.stat) {
                    cmLine.Update(key, value);
                }
                // contains
                if (this.heap.ContainsKey(key)) {
                    this.heap.ChangeValue(key, this.heap[key] + value);
                }
                else {
                    var keyV = this.ForceQuery(key);
                    if (this.heap.Count < this.w) {
                        this.heap.Add(key, keyV);
                    }
                    else {
                        var minFlow = this.heap.Min;
                        if (minFlow.Value < keyV) {
                            this.heap.ExtractMin();
                            this.heap.Add(key, keyV);
                        }
                    }
                }
            }


            private ElemType ForceQuery(object key) {
                var result = new List<ElemType>();
                foreach (CSLine cmLine in this.stat) {
                    var q = cmLine.Query(key);
                    if (q > 0)
                        result.Add(cmLine.Query(key));
                }

                if (result.Count == 0) return 0;
                var len = result.Count;
                if (len % 2 == 0) {
                    return (result[len / 2] + result[len / 2 - 1]) / 2;
                }
                else {
                    return result[(len - 1) / 2];
                }
            }

            public ElemType Query(object key) {
                if (!this.heap.ContainsKey(key)) {
                    return 0;
                }
                return this.heap[key];
                var result = new List<ElemType>();
                foreach (CSLine cmLine in this.stat) {
                    var q = cmLine.Query(key);
                    if (q > 0)
                        result.Add(cmLine.Query(key));
                }

                if (result.Count == 0) return 0;

                result.Sort();
                var len = result.Count;
                if (len % 2 == 0) {
                    return (result[len / 2] + result[len / 2 - 1]) / 2;
                }
                else {
                    return result[(len - 1) / 2];
                }
            }

            public List<ElemType> QueryList(object key) {
                if (!this.heap.ContainsKey(key)) {
                    return null;
                }
                var result = new List<ElemType>();
                foreach (CSLine cmLine in this.stat) {
                    result.Add(cmLine.Query(key));
                }
                return result;
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

        private static Random rnd = new Random();

        private Dictionary<Switch, SwitchSketch> data;
        public int W { get; private set; }
        private int d;

        public CountSketch(int _w, int _d) {
            this.W = _w;
            this.d = _d;
            this.data = new Dictionary<Switch, SwitchSketch>();
        }

        public void Update(Flow flow, ElemType value) {
            var t = value;
            var packet = 1750000;
            while (t > 0) {
                this._update(flow, t > packet ? packet : t);
                t -= packet;
            }
        }

        private void _update(Flow flow, ElemType value) {
            foreach (Switch sw in flow) {
                if (!this.data.ContainsKey(sw)) {
                    this.data.Add(sw, new SwitchSketch(this.W, this.d));
                }
                this.data[sw].Update(flow, value);
            }
        }

        public long Query(Flow flow) {
            List<ElemType> results = new List<ElemType>();
            foreach (var sw in flow) {
                var q = this.data[sw].QueryList(flow);
                if (q != null) {
                    results = results.Concat(q).ToList();
                }
            }
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

        public long this[Flow key] => this.Query(key);

        public IEnumerable<Flow> GetAllKeys() {
            var list = (IEnumerable<Flow>) new List<Flow>();
            foreach (var pair in this.data) {
                list = list.Concat(pair.Value.GetKeys<Flow>());
            }
            return new HashSet<Flow>(list);
        }
    }
}