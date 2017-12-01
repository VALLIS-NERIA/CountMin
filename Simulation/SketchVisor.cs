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
            private Dictionary<Flow, Entry> hashMap;

            public int K { get; private set; }
            public ElemType E { get; private set; }
            public ElemType V { get; private set; }

            protected ElemType ComputeThresh(IEnumerable<ElemType> list, double delta = 0.05) {
                var l1 = list.OrderByDescending(a => a);
                var a1 = l1.First();
                var a2 = l1.ElementAt(1);
                var ak = l1.Last();
                var b = (double) (a1 - 1) / (a2 - 1);
                var theta = b == 1 ? 1 : Log(0.5, b);
                var _e = Pow(1 - delta, 1 / theta) * ak;
                var __e = (ElemType) Round(_e);
                if (__e < 0) {
                    ;
                }
                return __e;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="f"></param>
            /// <param name="v"></param>
            /// <returns>true: kickout; false: no kickout</returns>
            public bool Update(Flow f, ElemType v) {
                checked {
                    this.V += v;
                    if (hashMap.ContainsKey(f)) {
                        hashMap[f] += (0, v, 0);
                        return false;
                    }
                    else if (hashMap.Count < K) {
                        hashMap[f] = new Entry {e = E, r = v, d = 0};
                        return false;
                    }
                    else {
                        var list = this.hashMap.Select(e => e.Value.r).Concat(new List<ElemType> {v});
                        var _e = ComputeThresh(list);
                        var readyRemove = new List<Flow>();
                        var keys = this.hashMap.Keys.ToList();
                        foreach (var key in keys) {
                            this.hashMap[key] += (0, -_e, +_e);
                            if (this.hashMap[key].r < 0) {
                                readyRemove.Add(key);
                            }
                        }
                        foreach (var key in readyRemove) {
                            this.hashMap.Remove(key);
                        }
                        if (v > _e && this.hashMap.Count < K) {
                            hashMap[f] = new Entry {e = E, r = v - _e, d = _e};
                        }
                        this.E += _e;
                        return true;
                    }
                }
            }

            public ElemType Query(Flow key) {
                if (this.hashMap.ContainsKey(key)) {
                    checked {
                        var ret = this.hashMap[key].r + this.hashMap[key].d + this.hashMap[key].e / 2;
                        if (ret < 0) {
                            Console.WriteLine();
                        }
                        return ret;
                    }
                }
                else {
                    return 0;
                }
            }

            public ElemType this[Flow key] => Query(key);

            public SwitchSketch(int k) {
                this.K = k;
                this.hashMap = new Dictionary<Flow, Entry>();
            }
        }

        private Dictionary<Switch, SwitchSketch> map;
        public int K { get; private set; }


        public SketchVisor() : this(1000) { }

        public SketchVisor(int k) {
            this.K = k;
            this.map = new Dictionary<Switch, SwitchSketch>();
        }

        public int TotalUpdate = 0;

        public int TotalKickout = 0;
        //public int FlowKickout = 0;

        public void Update(Flow key, long value) {
            foreach (Switch sw in key) {
                if (!this.map.ContainsKey(sw)) {
                    this.map.Add(sw, new SwitchSketch(this.K));
                }
                var kickout = this.map[sw].Update(key, value);
                if (kickout) {
                    this.TotalKickout += 1;
                }
                else {
                    this.TotalUpdate += 1;
                }
            }
            //Console.Write($"\rkickout:{TotalKickout} , update:{TotalUpdate}");
        }

        public ElemType Query(Flow key) {
            var result = new List<ElemType>();
            foreach (Switch sw in key) {
                result.Add(this.map[sw][key]);
            }
            result.Sort();
            var avg = (ElemType) result.Average();
            if (avg < 0) {
                Console.WriteLine();
            }
            return avg;
        }

        public ElemType this[Flow key] => Query(key);
    }
}