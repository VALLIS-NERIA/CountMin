using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation.Deployments {
    public class RuledSketch <T> : TopoSketchBase<T> where T : ISketch<object, long> {
        public IReadOnlyDictionary<Switch, IFlowRule> Rules { get; }

        public RuledSketch(SketchFactory<T> factory, Topology topo, int w, IReadOnlyDictionary<Switch, IFlowRule> rules) : base(factory, topo, w) {
            this.Rules = rules;
        }

        protected override Dictionary<Switch, T> DeploySketches() => this.topo.Switches.ToDictionary(sw => sw, sw => base.factory());

        public override void Update(Flow key, long value) {
            foreach (Switch sw in key.Switches) {
                if (this.Rules.ContainsKey(sw) && this.Rules[sw].ContainsFlow(key)) {
                    this.sketches[sw].Update(key, value);
                    this.throughput[sw] += value;
                }
            }
        }
    }
}