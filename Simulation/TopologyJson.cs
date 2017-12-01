using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {

    public class SwitchJson {
        public int num { get; set; }
        public List<int> linkedSwitches { get; set; }
        public string name { get; set; }
        //public List<double> trafficLoad { get; set; }

        public SwitchJson(int num,string name=null) {
            linkedSwitches=new List<int>();
            this.num = num;
            this.name = name;
        }

        public Switch ToSwitch(Topology topo) {
            var sw = topo.Switches[num];
            sw.Name = this.name;
            foreach (int i in linkedSwitches) {
                sw.Link(topo.Switches[i]);
            }
            return sw;
        }
    }

    public class TopologyJson {
        public List<SwitchJson> switches { get; set; }
        public int switchCount => switches.Count;
        //public List<string> switchName { get; set; }
        public TopologyJson() {
            switches = new List<SwitchJson>();
        }

        public Topology ToTopology() {
            var topo=new Topology();
            for (int i = 0; i < switchCount; i++) {
                topo.Switches.Add(new Switch(topo: topo));
            }
            foreach (SwitchJson swJ in switches) {
                swJ.ToSwitch(topo);
            }
            topo.FloydCalc();
            return topo;
        }
    }

    // public 
}