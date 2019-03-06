using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulation.Sketches {
    public class Line {
        private static Random rnd = new Random();

        private delegate uint HashFunc(object obj);

        public Int64[] Count;
        public object[] Keys;
        private int w;

        private HashFunc hash;

        private int seed;

        // not necessary in simulation
        // private Mutex mutex;
        private HashFunc hashFactory(int seed) { return o => (uint) (((uint) (o.GetHashCode() ^ seed)) % this.w); }

        public Line(int _w) {
            this.w = _w;
            this.Count = new Int64[this.w];
            this.Keys = new object[this.w];
            this.seed = rnd.Next();
            this.hash = this.hashFactory(this.seed);
        }

        public int Update(object key, Int64 value) {
            int index = (int) (uint) ((key.GetHashCode() ^ this.seed) % this.w);
            var f_ = this.Keys[index];
            if (f_ == key) {
                this.Count[index] += value;
            }
            else {
                if (this.Count[index] > value) {
                    this.Count[index] -= value;
                }
                else {
                    this.Count[index] = value - this.Count[index];
                    this.Keys[index] = key;
                }
            }

            return index;
        }

        public Int64 Query(object key) {
            if (this.Keys[this.hash(key)] == key) {
                return this.Count[this.hash(key)];
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

        public Int64 this[object key] => this.Query(key);
    }

    public class Sketch : ISketch<long> {
        private Line[] stat;
        private int w, d;

        public Sketch(int _w, int _d) {
            this.w = _w;
            this.d = _d;
            this.stat = new Line[this.d];
            for (int i = 0; i < this.d; i++) {
                this.stat[i] = (new Line(this.w));
            }
        }

        public void Update(object key, Int64 value) {
            for (int i = 0; i < this.d; i++) {
                this.stat[i].Update(key, value);
            }
        }

        public Int64 Query(object key) {
            var result = new List<Int64>();
            foreach (Line cmLine in this.stat) {
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

    public class CountMax : IReversibleSketch<Flow, long> {
        //public Type ElementType = new ElemType().GetType();

        public class CMLine : Line {
            public CMLine(int _w) : base(_w) { }
        }

        public class SwitchSketch : Sketch {
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
                this.data.Add(sw, new SwitchSketch(this.W, this.d));
            }
        }

        public CountMax(int w) : this(w, 1) { }

        public void Update(Flow flow, Int64 value) {
            var t = value;
            var packet = 500;
            while (t > 0) {
                this._update(flow, t > packet ? packet : t);
                t -= packet;
            }
        }

        private void _update(Flow flow, Int64 value) {
            foreach (Switch sw in flow) {
                //if (!data.ContainsKey(sw)) {
                //    data.Add(sw, new SwitchSketch(W, d));
                //}

                this.data[sw].Update(flow, value);
            }
        }

        public Int64 Query(Switch sw, Flow flow) { return this.data[sw].Query(flow); }

        public Int64 Query(Flow flow) {
            var result = new List<Int64>();
            foreach (Switch sw in flow) {
                result.Add(this.Query(sw, flow));
            }

            return result.Max();
        }

        public Int64 this[Flow key] => this.Query(key);

        public IEnumerable<Flow> GetAllKeys() {
            var list = (IEnumerable<Flow>) new List<Flow>();
            foreach (var pair in this.data) {
                list = list.Concat(pair.Value.GetAllKeys<Flow>());
            }

            return new HashSet<Flow>(list);
        }
    }
}