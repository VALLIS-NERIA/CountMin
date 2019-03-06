using System;
using System.Collections.Generic;
using System.Linq;
using Simulation.Sketches;

namespace Simulation.Deployments {
    using ElemType = Int64;

    public class HalfSketch <T> : TopoSketchBase<T> where T : class, ISketch<object, ElemType> {
        private static Random rnd = new Random();
        
        public HalfSketch(SketchFactory<T> factory, Topology topo, int w) : base(factory, topo, w) { }

        protected override Dictionary<Switch, T> DeploySketches() => this.topo.Switches.Where(sw => sw.IsEdge).ToDictionary(sw => sw, sw => base.factory());

        public override void Update(Flow flow, ElemType value) {
            Switch sw = rnd.NextDouble() < 0.5 ? flow.IngressSwitch : flow.OutgressSwitch;
            this.sketches[sw].Update(flow, value);
            this.throughput[sw] += value;
        }
    }
}