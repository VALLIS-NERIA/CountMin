using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Simulation {
    public class Topology {
        private static Random rnd = new Random();
        public List<Switch> Switches;
        public Topology() { Switches = new List<Switch>(); }

        public TopologyJson ToTopologyJson() {
            var json = new TopologyJson();
            for (int i = 0; i < Switches.Count; i++) {
                var sw = Switches[i];
                json.switches.Add(new SwitchJson(i, sw.Name));
                foreach (var sw2 in sw.LinkedSwitches) {
                    json.switches[i].linkedSwitches.Add(Switches.IndexOf(sw2));
                }
            }
            return json;
        }

        public Switch RandomSwitch() { return Switches[(rnd.Next(Switches.Count) / 2) * 2]; }

        public IEnumerable<KeyValuePair<(Switch src, Switch dst), double>> FetchLinkLoad() {
            var list = (IEnumerable<KeyValuePair<(Switch src, Switch dst), double>>) new Dictionary<(Switch src, Switch dst), double>();
            foreach (Switch sw in Switches) {
                list = list.Concat(sw.LinkLoad.ToDictionary(k => (sw, k.Key), k => k.Value));
            }
            return list;
        }
    }
}