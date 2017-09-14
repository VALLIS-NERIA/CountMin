using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class Flow : IEnumerable<Switch> {
        //public string name;
        public Switch IngressSwitch;

        public Switch OutgressSwitch;

        public List<Switch> Switches => Nodes;

        /// <summary>
        /// Jump nodes, or hops.
        /// </summary>
        public List<Switch> Nodes ;

        /// <summary>
        /// traffic. s(f).
        /// </summary>
        public double Traffic;


        public Flow()  { this.Nodes=new List<Switch>();}

        public Flow(List<Switch> nodes,double traffic) :this(nodes) { this.Traffic = traffic; }

        public Flow(List<Switch> nodes)  {
            Nodes = nodes;
            IngressSwitch = nodes.First();
            OutgressSwitch = nodes.Last();
        }

        public Flow(Flow other) {
            this.IngressSwitch = other.IngressSwitch;
            this.OutgressSwitch = other.OutgressSwitch;
            this.Nodes = new List<Switch>(other.Nodes);
        }

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


        public IEnumerator<Switch> GetEnumerator() { return Nodes.GetEnumerator(); }
        public override string ToString() { return $"{IngressSwitch} =={Traffic:F1}=> {OutgressSwitch}"; }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        //public string ToString(bool inShort) { return inShort ? $"{name}@{Switch.name}" : ToString(); }
    }
}