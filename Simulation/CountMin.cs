using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//using ElemType = System.Double;

namespace Simulation {
    public class Test <T> where T : IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T> {
        public T data;

        public void Set(T d) {
            data = d;
        }

        public void Add(T d) {
            if (d is int) {
                //object t = data;
                data = (T)(object)(Convert.ToInt32(data) + Convert.ToInt32(d));
            }
        }
    }



    public class CountMin <T> where T : struct {
        //public Type ElementType = new ElemType().GetType();
        private static Random rnd = new Random();

        private delegate int HashFunc(object obj);

        public delegate T AddFunc(T v1, T v2);
        public class CMLine {
            private T[] stat;
            private int w;

            private HashFunc hash;

            // not necessary in simulation
            // private Mutex mutex;
            private HashFunc hashFactory(int seed) { return o => ((o.GetHashCode() ^ seed) % w); }

            public CMLine(int _w) {
                this.w = _w;
                this.stat = new T[w];
                hash = hashFactory(rnd.Next());
            }

            public int Update(object key, T value, AddFunc add=null) {
                int index = hash(key);
                //stat[index] += value;
                if (value is short) {
                    stat[index] = (T) (object) (Convert.ToInt16(stat[index]) + Convert.ToInt16(value));
                }
                else if (value is int) {
                    stat[index] = (T) (object) (Convert.ToInt32(stat[index]) + Convert.ToInt32(value));
                }
                else if (value is long) {
                    stat[index] = (T) (object) (Convert.ToInt64(stat[index]) + Convert.ToInt64(value));
                }
                else if (value is float) {
                    stat[index] = (T) (object) (Convert.ToSingle(stat[index]) + Convert.ToSingle(value));
                }
                else if (value is double) {
                    stat[index] = (T) (object) (Convert.ToDouble(stat[index]) + Convert.ToDouble(value));
                }
                else if (value is decimal) {
                    stat[index] = (T) (object) (Convert.ToDecimal(stat[index]) + Convert.ToDecimal(value));
                }
                else {
                    stat[index] = add(stat[index], value);
                }
                return index;
            }

            public T Query(object key) { return stat[hash(key)]; }

            public T this[object key] => Query(key);
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

            public void Update(object key, T value, AddFunc add = null) {
                foreach (CMLine cmLine in stat) {
                    cmLine.Update(key, value, add);
                }
            }

            public T Query(object key) {
                var result = new List<T>();
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

        public CountMin(int _w, int _d, AddFunc add=null) {
            this.W = _w;
            this.d = _d;
            this.Add = add;
            var t = typeof(T);
            if (t != typeof(short) && t != typeof(int) && t != typeof(long) && t != typeof(float) && t != typeof(double) && t != typeof(decimal)) {
                if (add == null) {
                    throw new ArgumentException("");
                }
            }
            this.data=new Dictionary<Switch, SwitchSketch>();
        }

        public void Update(Flow flow, T value) {
            foreach (Switch sw in flow.Nodes) {
                if (!data.ContainsKey(sw)) {
                    data.Add(sw, new SwitchSketch(W, d));
                }
                data[sw].Update(flow, value, Add);
            }
        }

        public T Query(Switch sw, Flow flow) { return data[sw].Query(flow); }

        public T Query(Flow flow) {
            var result = new List<T>();
            foreach (Switch sw in flow.Nodes) {
                result.Add(Query(sw,flow));
            }
            return result.Min();
        }
    }
}