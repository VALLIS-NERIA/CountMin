using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class MscSketch {
        public class Wildcard {
            public HashSet<Flow> Flows;
        }

        public class SubWildcard {
            public Wildcard Wildcard;
            public Switch Switch;
            public int Value => this.ValuableFlows.Count;
            public int Cost { get; private set; }
            public HashSet<Flow> Flows;
            public HashSet<Flow> ValuableFlows;

            public SubWildcard(Wildcard wc, Switch sw, HashSet<Flow> flows) {
                this.Wildcard = wc;
                this.Switch = sw;
                this.Flows = flows;
            }

            public void Done() {
                this.ValuableFlows = new HashSet<Flow>(this.Flows);
                this.Cost = (int) this.Flows.Sum(f => f.Traffic);
            }

            // data determines which sub-wildcards are selected in switches.
            public void UpdateValue(HashSet<Flow> coveredFlows) {
                var rubbish = new HashSet<Flow>();
                foreach (Flow flow in this.ValuableFlows) {
                    if (coveredFlows.Contains(flow)) {
                        rubbish.Add(flow);
                    }
                }

                foreach (Flow dust in rubbish) {
                    this.ValuableFlows.Remove(dust);
                }
            }
        }

        private Topology topo;
        private List<Flow> flows;
        private HashSet<Wildcard> wildcards;
        private Dictionary<Switch, Dictionary<Wildcard, SubWildcard>> swcDict;

        public void Init() {
            var dict = new Dictionary<ValueTuple<Switch, Switch>, HashSet<Flow>>();

            foreach (Flow flow in this.flows) {
                var od = (flow.IngressSwitch, flow.OutgressSwitch);
                if (!dict.ContainsKey(od)) {
                    dict.Add(od, new HashSet<Flow> {flow});
                }
                else {
                    dict[od].Add(flow);
                }
            }

            this.wildcards = new HashSet<Wildcard>(dict.Values.Select(s => new Wildcard {Flows = s}));
            Debug.Assert(this.wildcards.Sum(wc => wc.Flows.Count) == this.flows.Count);

            this.swcDict = new Dictionary<Switch, Dictionary<Wildcard, SubWildcard>>();
            
            foreach (Wildcard wc in this.wildcards) {
                foreach (Flow f in wc.Flows) {
                    foreach (Switch sw in f.Switches) {
                        if (!this.swcDict.ContainsKey(sw)) {
                            this.swcDict.Add(
                                sw,
                                new Dictionary<Wildcard, SubWildcard>
                                {
                                    {
                                        wc,
                                        new SubWildcard(wc, sw, new HashSet<Flow>())
                                    }
                                });
                        }

                        if (!this.swcDict[sw].ContainsKey(wc)) {
                            this.swcDict[sw].Add(wc, new SubWildcard(wc, sw, new HashSet<Flow>()));
                        }

                        this.swcDict[sw][wc].Flows.Add(f);
                    }
                }
            }
        }

        private static HashSet<SubWildcard> GreedyKnapsack(Switch sw, int budget, HashSet<SubWildcard> items) {
            var pack = new HashSet<SubWildcard>();
            int weight = 0;
            foreach (var swc in items.OrderBy(i => i.Value)) {
                int cost = swc.Cost;
                if (weight + cost < budget) {
                    pack.Add(swc);
                }

                weight += cost;
            }

            return pack;
        }
    }
}