using System;
using System.Collections.Generic;
using System.Linq;
using ElemType = System.Int64;

namespace Simulation.CountMaxSketch {
    public class Line {
        private static Random rnd = new Random();

        private delegate uint HashFunc(object obj);

        public ElemType[] Count;
        public object[] Keys;
        private int w;

        private HashFunc hash;

        private int seed;

        // not necessary in simulation
        // private Mutex mutex;
        private HashFunc hashFactory(int seed) { return o => (uint) (((uint) (o.GetHashCode() ^ seed)) % this.w); }

        public Line(int _w) {
            this.w = _w;
            this.Count = new ElemType[w];
            this.Keys = new object[w];
            this.seed = rnd.Next();
            this.hash = hashFactory(seed);
        }

        public int Update(object key, ElemType value) {
            int index = (int) (uint) ((key.GetHashCode() ^ seed) % this.w);
            var f_ = Keys[index];
            if (f_ == key) {
                Count[index] += value;
            }
            else {
                if (Count[index] > value) {
                    Count[index] -= value;
                }
                else {
                    Count[index] = value - Count[index];
                    Keys[index] = key;
                }
            }

            return index;
        }

        public ElemType Query(object key) {
            if (Keys[hash(key)] == key) {
                return Count[hash(key)];
            }

            return 0;
        }

        public HashSet<T> GetKeys <T>() where T : class {
            var set = new HashSet<T>();
            foreach (object key in this.Keys) {
                if (!set.Contains(key as T)) {
                    set.Add(key as T);
                }
            }

            return set;
        }

        public ElemType this[object key] => Query(key);
    }

    public class Sketch : ISketch<ElemType> {
        private Line[] stat;
        private int w, d;

        public Sketch(int _w, int _d) {
            this.w = _w;
            this.d = _d;
            this.stat = new Line[d];
            for (int i = 0; i < d; i++) {
                stat[i] = (new Line(w));
            }
        }

        public void Update(object key, ElemType value) {
            for (int i = 0; i < d; i++) {
                this.stat[i].Update(key, value);
            }
        }

        public ElemType Query(object key) {
            var result = new List<ElemType>();
            foreach (Line cmLine in stat) {
                result.Add(cmLine.Query(key));
            }

            return result.Max();
        }

        public HashSet<T> GetAllKeys <T>() where T : class {
            var list = (IEnumerable<T>) new List<T>();
            foreach (Line line in this.stat) {
                list = list.Concat(line.GetKeys<T>());
            }

            return new HashSet<T>(list);
        }


    }
}

namespace Simulation {
    public class FilteredCountMax : IReversibleSketch<Flow, ElemType> {
        public class CMLine : CountMaxSketch.Line {
            public CMLine(int _w) : base(_w) { }
        }

        public class SwitchSketch : CountMaxSketch.Sketch {
            public SwitchSketch(int _w, int _d) : base(_w, _d) { }
        }

        private int threshold = 1000;

        private Dictionary<Switch, SwitchSketch> data;
        private Dictionary<Switch, CountMin.SwitchSketch> filter;
        public int W { get; }
        private int d;

        public FilteredCountMax(int _w, int _d, Topology topo) {
            this.W = _w;
            this.d = _d;
            this.data = new Dictionary<Switch, SwitchSketch>();
            this.filter = new Dictionary<Switch, CountMin.SwitchSketch>();
            this.Init(topo);
        }

        //public FilteredCountMax(int w) : this(w, 1) { }

        private void Init(Topology topo) {
            foreach (Switch sw in topo.Switches) {
                if (sw.IsEdge) {
                    this.data.Add(sw, new SwitchSketch(this.W, this.d));
                }
                else {
                    this.filter.Add(sw, new CountMin.SwitchSketch(this.W, this.d));
                }
            }
        }

        public void Update(Flow flow, ElemType value) {
            var t = value;
            var packet = 500;
            while (t > 0) {
                _update(flow, t > packet ? packet : t);
                t -= packet;
            }
        }

        private void _update(Flow flow, ElemType value) {
            bool large = false;
            foreach (Switch sw in flow) {
                if (!sw.IsEdge) {
                    //if (!this.filter.ContainsKey(sw)) {
                    //    this.filter.Add(sw, new CountMin.SwitchSketch(W, d));
                    //}

                    var amount = this.filter[sw].PeekUpdate(flow, value);
                    if (amount > this.threshold) {
                        large = true;
                        this.data[flow.OutgressSwitch].Update(flow, value);
                        break;
                    }
                }
                else if (large) {
                    //if (!data.ContainsKey(sw)) {
                    //    data.Add(sw, new SwitchSketch(W, d));
                    //}

                    data[sw].Update(flow, value);
                }
                else {
                    // Filtered.
                }
            }
        }

        public ElemType Query(Switch sw, Flow flow) { return data[sw].Query(flow); }

        public ElemType Query(Flow flow) {
            var result = new List<ElemType>();
            foreach (Switch sw in flow) {
                if (sw.IsEdge) {
                    result.Add(this.data[sw].Query(flow));
                }
            }

            return result.Max() + (long) (this.threshold * 0.5);
        }

        public ElemType this[Flow key] => this.Query(key);

        public IEnumerable<Flow> GetAllKeys() {
            var list = (IEnumerable<Flow>) new List<Flow>();
            foreach (var pair in this.data) {
                list = list.Concat(pair.Value.GetAllKeys<Flow>());
            }

            return new HashSet<Flow>(list);
        }
    }

    public class CountMax : IReversibleSketch<Flow, ElemType> {
        //public Type ElementType = new ElemType().GetType();

        public class CMLine : CountMaxSketch.Line {
            public CMLine(int _w) : base(_w) { }
        }

        public class SwitchSketch : CountMaxSketch.Sketch {
            public SwitchSketch(int _w, int _d) : base(_w, _d) { }


        }

        private Dictionary<Switch, SwitchSketch> data;
        public int W { get; }
        private int d;

        public CountMax(int _w, int _d) {
            this.W = _w;
            this.d = _d;
            this.data = new Dictionary<Switch, SwitchSketch>();
        }

        public void Init(Topology topo) {
            foreach (Switch sw in topo.Switches) {
                data.Add(sw, new SwitchSketch(W, d));
            }
        }

        public CountMax(int w) : this(w, 1) { }

        public void Update(Flow flow, ElemType value) {
            var t = value;
            var packet = 500;
            while (t > 0) {
                _update(flow, t > packet ? packet : t);
                t -= packet;
            }
        }

        private void _update(Flow flow, ElemType value) {
            foreach (Switch sw in flow) {
                //if (!data.ContainsKey(sw)) {
                //    data.Add(sw, new SwitchSketch(W, d));
                //}

                data[sw].Update(flow, value);
            }
        }

        public ElemType Query(Switch sw, Flow flow) { return data[sw].Query(flow); }

        public ElemType Query(Flow flow) {
            var result = new List<ElemType>();
            foreach (Switch sw in flow) {
                result.Add(Query(sw, flow));
            }

            return result.Max();
        }

        public ElemType this[Flow key] => this.Query(key);

        public IEnumerable<Flow> GetAllKeys() {
            var list = (IEnumerable<Flow>) new List<Flow>();
            foreach (var pair in this.data) {
                list = list.Concat(pair.Value.GetAllKeys<Flow>());
            }

            return new HashSet<Flow>(list);
        }
    }
}