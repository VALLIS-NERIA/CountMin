using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Deployments {
    public class Msc {
        public class Wildcard : IFlowRule {
            public Switch Source;
            public Switch Dest;
            public HashSet<Flow> Flows;

            public override string ToString() =>$"{this.Source} => {this.Dest}";

            public bool ContainsFlow(Flow f) {
                return f.IngressSwitch == this.Source && f.OutgressSwitch == this.Dest;
            }
        }

        public class ODRule : IFlowRule {
            private HashSet<ODPair> hs;

            public ODRule(IEnumerable<Wildcard> wcs) {
                this.hs = new HashSet<ODPair>(wcs.Select(wc => new ODPair {Source = wc.Source, Dest = wc.Dest}));
            }

            public bool ContainsFlow(Flow f) {
                return this.hs.Contains(new ODPair {Source = f.IngressSwitch, Dest = f.OutgressSwitch});
            }
        }

        public struct ODPair {
            public Switch Source;
            public Switch Dest;
        }

        private class SubWildcard {
            public Wildcard Wildcard;
            public Switch Switch;
            public int Value { get; private set; }
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
                this.Value = (int) this.ValuableFlows.Sum(f => f.Traffic);
            }

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

                this.Value = (int) this.ValuableFlows.Sum(f => f.Traffic);
            }
        }

        private class KnapsackResult {
            public Switch Sw;
            public IEnumerable<SubWildcard> Swcs;
            public int Value;
            //public int Cost;
        }

        /* input */
        private readonly int budget;
        private readonly Topology topo;
        private readonly IReadOnlyCollection<Flow> flows;

        /* middle results */
        private HashSet<Wildcard> wildcards;
        private Dictionary<Switch, Dictionary<Wildcard, SubWildcard>> swcDict;
        private Task gmscTask;

        /* output */
        private readonly Dictionary<Switch, IEnumerable<Wildcard>> rules;
        private readonly HashSet<Flow> coveredFlows;

        public long AlgoTime { get; }

        public Dictionary<Switch, IFlowRule> GetRules() {
            return this.rules.ToDictionary(p => p.Key, p => (IFlowRule) new ODRule(p.Value));
        }

        public HashSet<Flow> GetCoveredFlows() {
            return this.coveredFlows;
        }

        public Msc(Topology topo, IReadOnlyCollection<Flow> flows, int budget) {
            this.topo = topo;
            this.flows = flows;
            this.budget = budget;
            this.rules = new Dictionary<Switch, IEnumerable<Wildcard>>();
            this.swcDict = new Dictionary<Switch, Dictionary<Wildcard, SubWildcard>>();
            this.coveredFlows = new HashSet<Flow>();

            this.DivideWildcards();
            var t = DateTime.Now;
            this.GMSC();
            this.AlgoTime = (long)(DateTime.Now - t).TotalMilliseconds;
        }


        private void DivideWildcards() {
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

            this.wildcards = new HashSet<Wildcard>(dict.Select(pair => new Wildcard {Source = pair.Key.Item1, Dest = pair.Key.Item2, Flows = pair.Value}));
            Debug.Assert(this.wildcards.Sum(wc => wc.Flows.Count) == this.flows.Count);

            foreach (Switch sw in this.topo.Switches) {
                this.swcDict[sw] = new Dictionary<Wildcard, SubWildcard>();
            }

            foreach (Wildcard wc in this.wildcards) {
                foreach (Flow f in wc.Flows) {
                    foreach (Switch sw in f.Switches) {
                        if (!this.swcDict[sw].ContainsKey(wc)) {
                            this.swcDict[sw][wc] = new SubWildcard(wc, sw, new HashSet<Flow>());
                        }

                        this.swcDict[sw][wc].Flows.Add(f);
                    }
                }
            }

            foreach (Switch sw in this.topo.Switches) {
                foreach (Wildcard wc in this.wildcards) {
                    if (this.swcDict.ContainsKey(sw) && this.swcDict[sw].ContainsKey(wc)) {
                        this.swcDict[sw][wc].Done();
                    }
                }
            }
        }



        // returns the covered flows.
        private void GMSC() {
            var V = new HashSet<Switch>(this.topo.Switches);
            while (V.Count > 0) {
                var results = V.Select(sw => DpKnapsack(sw, this.budget, this.swcDict[sw].Values)).OrderByDescending(r => r.Value);
                var top = results.First();
                this.rules[top.Sw] = top.Swcs.Select(swc => swc.Wildcard);
                this.coveredFlows.UnionWith(top.Swcs.SelectMany(swc => swc.Flows));
                V.Remove(top.Sw);
                foreach (Switch sw in V) {
                    foreach (var pair in this.swcDict[sw]) {
                        pair.Value.UpdateValue(this.coveredFlows);
                    }
                }
            }
        }

        private static KnapsackResult GreedyKnapsack(Switch sw, int budget, IEnumerable<SubWildcard> items) {
            var pack = new HashSet<SubWildcard>();
            int weight = 0;
            int value = 0;
            foreach (var swc in items.OrderByDescending(i => i.Value)) {
                int cost = swc.Cost;
                if (weight + cost < budget) {
                    pack.Add(swc);
                    value += swc.Value;
                    weight += cost;
                }
            }

            return new KnapsackResult {Sw = sw, Swcs = pack, Value = value};
        }

        private class DpItem {
            private int _value;
            private HashSet<SubWildcard> items;

            public IEnumerable<SubWildcard> Items => this.items;

            public int Value => _value;

            public DpItem() {
                this.items = new HashSet<SubWildcard>();
                this._value = 0;
            }

            public void ReplaceWith(DpItem other) {
                this.items = new HashSet<SubWildcard>(other.items);
                this._value = other._value;
            }

            public bool Add(SubWildcard swc) {
                bool ret = this.items.Add(swc);
                if (ret) {
                    this._value += swc.Value;
                }

                return ret;
            }
        }

        private static KnapsackResult DpKnapsack(Switch sw, int budget, IEnumerable<SubWildcard> items) {
            //budget = budget / 1000 + 1;


            var dp = new DpItem[budget+1];
            for (int j = 0; j <= budget; j++) {
                dp[j] = new DpItem();
            }

            int i = 0;
            foreach (var item in items) {
                i++;
                int value = item.Value;
                int weight = (int) (item.Cost / 1f);
                for (int v = budget; v >= weight; v--) {
                    var with = dp[v - weight];
                    if (with.Value + value > dp[v].Value) {
                        dp[v].ReplaceWith(with);
                        dp[v].Add(item);
                    }
                }
            }

            return new KnapsackResult {Sw = sw, Swcs = dp[budget].Items, Value = dp[budget].Value};
        }
    }
}