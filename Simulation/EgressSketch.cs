using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    using ElemType = Int64;

    public class EgressSketch <T> : IFS, ITopoSketch<Flow, ElemType> where T : class, ISketch<ElemType> {
        private static Random rnd = new Random();


        private Dictionary<Switch, T> data = new Dictionary<Switch, T>();
        private SketchFactory<T> factoryMethod;

        public int W { get; private set; }
        public int D { get; private set; }

        public string SketchClassName => typeof(T).DeclaringType.Name;

        public EgressSketch(int w, int d, int threshold, SketchFactory<T> factoryMethod) {
            this.factoryMethod = factoryMethod;
            this.W = w;
            this.D = d;
        }

        public void Init(Topology topo) {
            foreach (var sw in topo.Switches) {
                if (sw.IsEdge) {
                    this.data.Add(sw, factoryMethod());
                }
            }
        }

        public void Update(Flow flow, ElemType value) {
            this.data[flow.OutgressSwitch].Update(flow, value);
        }

        public ElemType Query(Flow flow) { return this.data[flow.OutgressSwitch].Query(flow); }

        public ElemType this[Flow key] => this.Query(key);
    }
}