using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    using ElemType = Int64;

    public delegate T SketchFactory <out T>() where T : ISketch<ElemType>;

    public interface IFS : ITopoSketch<Flow, ElemType>  {
        string SketchClassName { get; }

        void Init(Topology topo);
    }

    public class FilteredSketch <T> : IFS, ITopoSketch<Flow, ElemType> where T : class, ISketch<ElemType> {
        private int _threshold = 1000;

        private Dictionary<Switch, T> data = new Dictionary<Switch, T>();
        private Dictionary<Switch, CountMin.SwitchSketch> filter = new Dictionary<Switch, CountMin.SwitchSketch>();
        private SketchFactory<T> factoryMethod;

        public int W { get; private set; }
        public int D { get; private set; }

        public string SketchClassName => typeof(T).DeclaringType.Name;

        public FilteredSketch(int w, int d, int threshold, SketchFactory<T> factoryMethod) {
            this._threshold = threshold;
            this.factoryMethod = factoryMethod;
            this.W = w;
            this.D = d;

        }

        public void Init(Topology topo) {

            foreach (var sw in topo.Switches) {
                if (sw.IsEdge) {
                    this.data.Add(sw, factoryMethod());
                }
                else {
                    this.filter.Add(sw, new CountMin.SwitchSketch(this.W, this.D));
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

                    if (amount + value > this._threshold) {
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
            if (!flow.OutgressSwitch.IsEdge) throw new ArgumentException("The flow's egress switch is not edge");
            return this.data[flow.OutgressSwitch].Query(flow) + (long) (this._threshold * 0.0);
        }

        public ElemType this[Flow key] => this.Query(key);
    }
}