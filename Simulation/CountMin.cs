using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElemType = System.Int64;

namespace Simulation {
    public interface ISketch <TKey, TValue> {
        void Update(TKey key, TValue value);
        TValue Query(TKey key);
        TValue this[TKey key] { get; }
    }


    public class CountMin : ISketch<Flow, ElemType> {
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
                this.stat = new ElemType[w];
                hash = hashFactory(rnd.Next());
            }

            public int Update(object key, ElemType value, AddFunc add = null) {
                int index = hash(key);
                stat[index] += value;
                return index;
            }

            public ElemType Query(object key) { return stat[hash(key)]; }

            public ElemType this[object key] => Query(key);
        }

        public class SwitchSketch {
            private List<CMLine> stat;
            private int w, d;

            public SwitchSketch(int _w, int _d) {
                this.w = _w;
                this.d = _d;
                this.stat = new List<CMLine>();
                for (int i = 0; i < d; i++) {
                    stat.Add(new CMLine(w));
                }
            }

            public void Update(object key, ElemType value, AddFunc add = null) {
                foreach (CMLine cmLine in stat) {
                    cmLine.Update(key, value, add);
                }
            }

            public ElemType Query(object key) {
                var result = new List<ElemType>();
                foreach (CMLine cmLine in stat) {
                    result.Add(cmLine.Query(key));
                }
                return result.Min();
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
                if (!data.ContainsKey(sw)) {
                    data.Add(sw, new SwitchSketch(W, d));
                }
                data[sw].Update(flow, value, Add);
            }
        }

        public ElemType Query(Switch sw, Flow flow) { return data[sw].Query(flow); }

        public ElemType Query(Flow flow) {
            var result = new List<ElemType>();
            foreach (Switch sw in flow.Nodes) {
                result.Add(Query(sw, flow));
            }
            return result.Min();
        }

        public ElemType this[Flow key] => Query(key);
    }
}