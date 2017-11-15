using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;
using static System.Math;

namespace Simulation {
    using ElemType = System.Int64;

    public class SketchVisor : ISketch<Flow, ElemType> {
        protected class Entry {
            public ElemType e;
            public ElemType r;
            public ElemType d;

            public static Entry operator +(Entry left, (ElemType _e, ElemType _r, ElemType _d) right) {
                return new Entry {e = left.e + right._e, r = left.r + right._r, d = left.d + right._d};
            }

            public static implicit operator Entry((ElemType _e, ElemType _r, ElemType _d) right) { return new Entry {e = right._e, d = right._d, r = right._r}; }
        }

        public class SwitchSketch {
            private Dictionary<Flow, Entry> map;

            public int K { get; private set; }
            public ElemType E { get; private set; }
            public ElemType V { get; private set; }

            protected ElemType ComputeThresh(IEnumerable<ElemType> list, double delta = 0.05) {
                var l1 = list.OrderByDescending(a => a);
                var a1 = l1.First();
                var a2 = l1.ElementAt(1);
                var ak = l1.Last();
                var b = (double) (a1 - 1) / (a2 - 1);
                var theta = Log(0.5, b);
                var _e = Pow(1 - delta, 1 / theta);
                return (ElemType) Round(_e);
            }

            public void Update(Flow f, ElemType v) {
                this.V += v;
                if (map.ContainsKey(f)) {
                    map[f] += (0, v, 0);
                }
                else if (map.Count < K) {
                    map[f] = new Entry {e = E, r = v, d = 0};
                }
                else {
                    var list = this.map.Select(e => e.Value.r).Concat(new List<ElemType> {v});
                    var _e = ComputeThresh(list);
                    var readyRemove = new List<Flow>();
                    var keys = this.map.Keys.ToList();
                    foreach (var key in keys) {
                        this.map[key] += (0, -_e, +_e);
                        if (this.map[key].r < 0) {
                            readyRemove.Add(key);
                        }
                    }
                    foreach (Flow flow in readyRemove) {
                        this.map.Remove(flow);
                    }
                    if (v > _e && this.map.Count < K) {
                        map[f] = new Entry {e = E, r = v - _e, d = _e};
                    }
                    this.E += _e;
                }
            }

            public ElemType Query(Flow key) {
                if (this.map.ContainsKey(key)) {
                    return this.map[key].r + this.map[key].d + this.map[key].e / 2;
                }
                else {
                    return 0;
                }
            }

            public ElemType this[Flow key] => Query(key);

            public SwitchSketch(int k) {
                this.K = k;
                this.map = new Dictionary<Flow, Entry>();
            }
        }

        private Dictionary<Switch, SwitchSketch> map;
        public int K { get; private set; }


        public SketchVisor() : this(1000) { }

        public SketchVisor(int k) {
            this.K = k;
            this.map = new Dictionary<Switch, SwitchSketch>();
        }

        public void Update(Flow key, long value) {
            foreach (Switch sw in key) {
                if (!this.map.ContainsKey(sw)) {
                    this.map.Add(sw,new SwitchSketch(this.K));
                }
                this.map[sw].Update(key, value);
            }
        }

        public ElemType Query(Flow key) {
            var result = new List<ElemType>();
            foreach (Switch sw in key) {
                result.Add(this.map[sw][key]);
            }
            result.Sort();
            var avg = (ElemType) result[result.Count / 2];
            return avg;
        }

        public ElemType this[Flow key] => Query(key);
    }
}