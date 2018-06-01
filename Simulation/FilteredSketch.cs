using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    using ElemType = Int64;

    public delegate T SketchFactory <T>() where T : ISketch<ElemType>;

    public class FilteredSketch <T> where T : ISketch<ElemType> {
        private int _threshold = 1000;

        private Dictionary<Switch, T> data = new Dictionary<Switch, T>();
        private Dictionary<Switch, CountMin.SwitchSketch> filter = new Dictionary<Switch, CountMin.SwitchSketch>();
        public int W { get; private set; }
        public int D { get; private set; }

        public FilteredSketch(int threshold) { this._threshold = threshold; }

        public void Init(Topology topo, int w, int d, SketchFactory<T> factoryMethod) {
            this.W = w;
            this.D = d;
            foreach (var sw in topo.Switches) {
                if (sw.IsEdge) {
                    this.data.Add(sw, factoryMethod());
                }
                else {
                    this.filter.Add(sw, new CountMin.SwitchSketch(w, d));
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

                    if (amount+value > this._threshold) {
                        if (amount < this._threshold) {
                            large = true;
                            this.data[flow.OutgressSwitch].Update(flow, value + amount);
                        }
                        else {
                            large = true;
                            this.data[flow.OutgressSwitch].Update(flow, value);
                        }

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

        public ElemType Query(Flow flow) {
            //var result = new List<ElemType>();
            //foreach (Switch sw in flow) {
            //    if (sw.IsEdge) {
            //        result.Add(this.data[sw].Query(flow));
            //    }
            //}

            //return result.Max() + (long)(this._threshold * 0.5);
            if(!flow.OutgressSwitch.IsEdge)throw new ArgumentException("The flow's egress switch is not edge");
            return this.data[flow.OutgressSwitch].Query(flow) + (long) (this._threshold * 0);
        }

        public ElemType this[Flow key] => this.Query(key);
    }
}