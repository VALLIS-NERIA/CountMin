using System;
using System.Collections.Generic;
using System.Linq;
using ElemType = System.Int64;

namespace Simulation.Sketches {

    public class CountMin : ITopoSketch<Flow, ElemType> {
        //public Type ElementType = new ElemType().GetType();
        private static Random rnd = new Random();

        private delegate int HashFunc(object obj);

        public delegate ElemType AddFunc(ElemType v1, ElemType v2);

        public class CMLine {
            private ElemType[] stat;
            private int w;

            private HashFunc hash;

            // not necessary in simulation
            // private Mutex mutex;
            private HashFunc hashFactory(int seed) { return o => (o.GetHashCode() ^ seed) % this.w; }

            public CMLine(int _w) {
                this.w = _w;
                this.stat = new ElemType[this.w];
                this.hash = this.hashFactory(rnd.Next());
            }

            public int Update(object key, ElemType value, AddFunc add = null) {
                int index = this.hash(key);
                this.stat[index] += value;
                return index;
            }

            public ElemType PeekUpdate(object key, ElemType value) {
                int index = this.hash(key);
                ElemType old = this.stat[index];
                this.stat[index] += value;
                return old;
            }

            public void Set(object key, ElemType value) {
                int index = this.hash(key);
                this.stat[index] = value;
            }

            public ElemType Query(object key) { return this.stat[this.hash(key)]; }

            public ElemType this[object key] => this.Query(key);
        }

        public class SwitchSketch {
            private List<CMLine> stat;
            private int w, d;

            public SwitchSketch(int _w, int _d) {
                this.w = _w;
                this.d = _d;
                this.stat = new List<CMLine>();
                for (int i = 0; i < this.d; i++) {
                    this.stat.Add(new CMLine(this.w));
                }
            }

            public void Update(object key, ElemType value, AddFunc add = null) {
                foreach (CMLine cmLine in this.stat) {
                    cmLine.Update(key, value, add);
                }
            }

            public ElemType PeekUpdate(object key, ElemType value) {
                ElemType min = long.MaxValue;
                foreach (CMLine cmLine in this.stat) {
                    var t = cmLine.PeekUpdate(key, value);
                    if (t < min) min = t;
                }

                return min;
            }

            public ElemType Query(object key) {
                var result = new List<ElemType>();
                foreach (CMLine cmLine in this.stat) {
                    result.Add(cmLine.Query(key));
                }
                return result.Min();
            }

            public void Set(object key, ElemType value) {
                foreach (CMLine cmLine in this.stat) {
                    cmLine.Set(key, value);
                }
            }
        }

        private Dictionary<Switch, SwitchSketch> data;
        public int W { get; }
        private int d;
        internal AddFunc Add;

        public CountMin(int _w, int _d, AddFunc add = null) {
            this.W = _w;
            this.d = _d;
            this.Add = add;
            var t = typeof(ElemType);
            if (t != typeof(short) && t != typeof(int) && t != typeof(long) && t != typeof(float) && t != typeof(double) && t != typeof(decimal)) {
                if (add == null) {
                    throw new ArgumentException("");
                }
            }
            this.data = new Dictionary<Switch, SwitchSketch>();
        }

        public CountMin(int w) : this(w, 1, null) { }

        public void Update(Flow flow, ElemType value) {
            foreach (Switch sw in flow.Nodes) {
                if (!this.data.ContainsKey(sw)) {
                    this.data.Add(sw, new SwitchSketch(this.W, this.d));
                }
                this.data[sw].Update(flow, value, this.Add);
            }
        }

        public ElemType Query(Switch sw, Flow flow) { return this.data[sw].Query(flow); }

        public ElemType Query(Flow flow) {
            var result = new List<ElemType>();
            foreach (Switch sw in flow.Nodes) {
                result.Add(this.Query(sw, flow));
            }
            return result.Min();
        }

        public ElemType this[Flow key] => this.Query(key);
    }
}