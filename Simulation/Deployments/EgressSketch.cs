using System;
using System.Collections.Generic;
using System.Linq;

namespace Simulation.Deployments {
    using ElemType = Int64;

    public class EgressSketch <T> :TopoSketchBase<T> where T : class, ISketch<object,ElemType> {
        private static Random rnd = new Random();

        public EgressSketch(SketchFactory<T> factory, Topology topo, int w) : base(factory, topo, w) { }

        protected override Dictionary<Switch, T> DeploySketches() => this.topo.Switches.Where(sw => sw.IsEdge).ToDictionary(sw => sw, sw => base.factory());

        public override void Update(Flow flow, ElemType value) {
            this.sketches[flow.OutgressSwitch].Update(flow, value);
            this.throughput[flow.OutgressSwitch] += value;
        }

        public override ElemType Query(Flow flow) { return this.sketches[flow.OutgressSwitch].Query(flow); }
    }
}