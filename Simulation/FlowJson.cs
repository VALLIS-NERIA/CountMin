using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class FlowJson {
        public List<int> path { get; set; }

        [DefaultValue(-1d)]
        public double traffic { get; set; }

        public FlowJson() { path = new List<int>(); }

        public Flow ToFlow(Topology topo) {
            var f = new Flow();
            foreach (var swIdx in this.path) {
                f.Nodes.Add(topo.Switches[swIdx]);
            }
            f.IngressSwitch = f.Nodes.First();
            f.OutgressSwitch = f.Nodes.Last();
            f.Traffic = this.traffic;
            return f;
        }
    }

    public class CoflowJson {
        public List<FlowJson> flows { get; set; }

        public CoflowJson() { flows = new List<FlowJson>(); }

        public List<Flow> ToCoflow(Topology topo) {
            var coflow = new List<Flow>();
            foreach (var flowJson in this.flows) {
                coflow.Add(flowJson.ToFlow(topo));
            }
            return coflow;
        }
    }

    public static partial class Helper {
        public static CoflowJson ToCoflowJson(this List<Flow> coflow, Topology topo) {
            var json = new CoflowJson();
            foreach (Flow flow in coflow) {
                json.flows.Add(flow.ToFlowJson(topo));
            }
            return json;
        }
    }
}