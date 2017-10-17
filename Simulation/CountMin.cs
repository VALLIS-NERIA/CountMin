using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ElemType = System.Double;

namespace Simulation {
    public class CountMin  {
        public Type ElementType = new ElemType().GetType();
        private static Random rnd=new Random();
        private delegate int HashFunc(object obj);

        public class CMLine {
            private ElemType[] stat;
            private int w;

            private HashFunc hash;

            // not necessary in simulation
            //private Mutex mutex;
            private HashFunc hashFactory(int seed) { return o => ((o.GetHashCode() ^ seed) % w); }

            public CMLine(int _w) {
                this.w = _w;
                this.stat = new ElemType[w];
                hash = hashFactory(rnd.Next());
            }

            public int Update(object key, ElemType value) {
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
                this.stat=new List<CMLine>();
                for (int i = 0; i < d; i++) {
                    stat.Add(new CMLine(w));
                }
            }

            public void Update(object key, ElemType value) {
                foreach (CMLine cmLine in stat) {
                    cmLine.Update(key, value);
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
        private int w, d;

        public CountMin(int _w, int _d) {
            this.w = _w;
            this.d = _d;
            this.data=new Dictionary<Switch, SwitchSketch>();
        }

        public void Update(Flow flow, ElemType value) {
            foreach (Switch sw in flow.Nodes) {
                if (!data.ContainsKey(sw)) {
                    data.Add(sw, new SwitchSketch(w, d));
                }
                data[sw].Update(flow, value);
            }
        }

        public ElemType Query(Switch sw, Flow flow) { return data[sw].Query(flow); }

        public ElemType Query(Flow flow) {
            var result = new List<ElemType>();
            foreach (Switch sw in flow.Nodes) {
                result.Add(Query(sw,flow));
            }
            return result.Min();
        }
    }
}