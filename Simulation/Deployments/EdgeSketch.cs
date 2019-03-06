using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElemType = System.Int64;

namespace Simulation.Deployments {
    public class EdgeSketch <T> : TopoSketchBase<T> where T : ISketch<object, ElemType> {
        public EdgeSketch(SketchFactory<T> factory, Topology topo, int w) : base(factory, topo, w) { }

        protected override Dictionary<Switch, T> DeploySketches() => this.topo.Switches.Where(sw => sw.IsEdge).ToDictionary(sw => sw, sw => base.factory());

        public override long Query(Flow key) {
            var res = new List<ElemType>();
            foreach (Switch sw in key.Switches) {
                if (!this.sketches.ContainsKey(sw)) continue;

                var q = this.sketches[sw].Query(key);
                if (q > 0) res.Add(q);
            }

            if (res.Count == 0) return 0;
            if (res.Count == 1) return res[0];
            return (ElemType) res.Median();
        }
    }
}