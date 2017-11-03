using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulation {
    using ElemType = System.UInt64;

    public class CountMax <T, U> :ISketch<T,ElemType> where T : IEnumerable<U> {
        //public Type ElementType = new ElemType().GetType();
        private static Random rnd = new Random();

        private delegate uint HashFunc(object obj);

        public delegate ElemType AddFunc(ElemType v1, ElemType v2);

        public class CMLine {
            public ElemType[] Count;
            public object[] Keys;
            private int w;

            private HashFunc hash;

            // not necessary in simulation
            // private Mutex mutex;
            private HashFunc hashFactory(int seed) { return o => (uint) (((uint) (o.GetHashCode() ^ seed)) % this.w); }

            public CMLine(int _w) {
                this.w = _w;
                this.Count = new ElemType[w];
                this.Keys = new object[w];
                hash = hashFactory(rnd.Next());
            }

            public int Update(object key, ElemType value, AddFunc add = null) {
                int index = (int) hash(key);
                var f_ = Keys[index];
                int flag = 0;
                ElemType ori = Count[index];
                if (f_ == key) {
                    Count[index] += value;
                    flag = 1;
                }
                else {
                    if (Count[index] > value) {
                        Count[index] -= value;
                        flag = 2;
                    }
                    else {
                        Count[index] = value - Count[index];
                        Keys[index] = key;
                        flag = 3;
                    }
                }
                if (Keys[hash(key)] == key && Count[hash(key)] > ((Flow) key).Traffic) {
                    throw new Exception();
                }
                return index;
            }

            public ElemType Query(object key) {
                if (Keys[hash(key)] == key) {
                    if (Count[hash(key)] > ((Flow) key).Traffic) {
                        throw new Exception();
                    }
                    return Count[hash(key)];
                }
                return 0;
            }

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
                return result.Max();
            }
        }

        private Dictionary<U, SwitchSketch> data;
        public int W { get; }
        private int d;
        internal AddFunc Add;

        public CountMax(int _w, int _d, AddFunc add = null) {
            this.W = _w;
            this.d = _d;
            this.Add = add;
            var t = typeof(ElemType);
            this.data = new Dictionary<U, SwitchSketch>();
        }

        public void Update(T flow, ElemType value) {
            foreach (U sw in flow) {
                if (!data.ContainsKey(sw)) {
                    data.Add(sw, new SwitchSketch(W, d));
                }
                data[sw].Update(flow, value, Add);
            }
        }

        public ElemType Query(U sw, T flow) { return data[sw].Query(flow); }

        public ElemType Query(T flow) {
            var result = new List<ElemType>();
            foreach (U sw in flow) {
                result.Add(Query(sw, flow));
            }
            return result.Max();
        }

        public ulong this[T key] => this.Query(key);
    }
}