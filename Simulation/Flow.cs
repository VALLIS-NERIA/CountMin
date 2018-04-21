using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class Flow : IEnumerable<Switch> {
        //public string name;
        public Switch IngressSwitch => Nodes.First();

        public Switch OutgressSwitch => Nodes.Last();

        public List<Switch> Switches => Nodes;

        /// <summary>
        /// Jump nodes, or hops.
        /// </summary>
        public List<Switch> Nodes;

        /// <summary>
        /// traffic. s(f).
        /// </summary>
        public double Traffic;


        public Flow() { this.Nodes = new List<Switch>(); }

        public Flow(List<Switch> nodes, double traffic) : this(nodes) { this.Traffic = traffic; }

        public Flow(List<Switch> nodes) { Nodes = nodes; }

        public Flow(Flow other) { this.Nodes = new List<Switch>(other.Nodes); }

        public FlowJson ToFlowJson(Topology topo) {
            var json = new FlowJson();
            foreach (var sw in this.Nodes) {
                if (!topo.Switches.Contains(sw)) {
                    throw new ArgumentException("This flow isn't in the given topology");
                }
                var swIdx = topo.Switches.IndexOf(sw);
                json.path.Add(swIdx);
                json.traffic = this.Traffic;
            }
            return json;
        }

        public void Assign() {
            foreach (Switch node in Nodes) {
                node.AssignFlow(this);
            }
        }

        public void ReAssign(List<Switch> newRoute) {
            this.Free();
            this.Nodes = newRoute;
            this.Assign();
        }

        public void OverrideAssign(List<Switch> newRoute) {
            this.Nodes = newRoute;
            this.Assign();
        }

        public void Free() {
            foreach (Switch node in Nodes) {
                node.RemoveFlow(this);
            }
        }

        public double GetMaxLoad() {
            double load = 0;
            using (var iter = GetEnumerator()) {
                iter.MoveNext();
                while (true) {
                    var sw = iter.Current;
                    if (!iter.MoveNext()) {
                        break;
                    }
                    var sw2 = iter.Current;
                    var cload = sw.LinkLoad[sw2];
                    if (cload > load) {
                        load = cload;
                    }
                }
                return load;
            }
        }

        public double GetTotalLoad() {
            double load = 0;
            using (var iter = GetEnumerator()) {
                iter.MoveNext();
                while (true) {
                    var sw = iter.Current;
                    if (!iter.MoveNext()) {
                        break;
                    }
                    var sw2 = iter.Current;
                    var cload = sw.LinkLoad[sw2];
                    load += cload;
                }
                return load;
            }
        }

        //public override 

        public IEnumerator<Switch> GetEnumerator() { return Nodes.GetEnumerator(); }
        public override string ToString() { return $"{IngressSwitch} =={Traffic:F1}=> {OutgressSwitch}"; }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        //public string ToString(bool inShort) { return inShort ? $"{name}@{Switch.name}" : ToString(); }
    }
}