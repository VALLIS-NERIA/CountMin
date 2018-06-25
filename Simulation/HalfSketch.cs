using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    using ElemType = Int64;

    public class HalfSketch<T> : IFS, ITopoSketch<Flow, ElemType> where T : class, ISketch<ElemType> {
        private static Random rnd= new Random();
        

        private Dictionary<Switch, T> data = new Dictionary<Switch, T>();
        private Dictionary<Switch, CountMin.SwitchSketch> filter = new Dictionary<Switch, CountMin.SwitchSketch>();
        private SketchFactory<T> factoryMethod;

        public int W { get; private set; }
        public int D { get; private set; }

        public string SketchClassName => typeof(T).DeclaringType.Name;

        public HalfSketch(int w, int d, int threshold, SketchFactory<T> factoryMethod) {
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
            if (rnd.NextDouble() < 0.5) {
                this.data[flow.IngressSwitch].Update(flow, value);
            }
            else {
                this.data[flow.OutgressSwitch].Update(flow, value);
            }

        }

        public ElemType Query(Flow flow) {
            //return result.Max() + (long)(this._threshold * 0.5);
            //if (!flow.OutgressSwitch.IsEdge) throw new ArgumentException("The flow's egress switch is not edge");
            return Math.Max(this.data[flow.OutgressSwitch].Query(flow), this.data[flow.IngressSwitch].Query(flow));
        }

        public ElemType this[Flow key] => this.Query(key);
    }
}
