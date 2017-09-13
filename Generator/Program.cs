using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Simulation;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;

namespace Generator {
    class Program {
        static Random rnd = new Random();

        static void Main(string[] args) {
            //var topo = HyperXGen();
            //var tJ = topo.ToTopologyJson();
            //var json = JsonConvert.SerializeObject(tJ);
            //using (var sw = new StreamWriter("hyperx.json")) {
            //    sw.Write(json);
            //}
            Topology topo = JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText("hyperx.json")).ToTopology();
            //foreach (var t in new[] {0.001, 0.01, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1}) {
                int i = 10000;
                var flowSet = new List<Flow>();


                while (i-- > 0) {
                    double traffic = rnd.NextDouble() < 0.2 ? 16 : 1;
                    flowSet.Add(RandomFlow(topo, 3, traffic));
                }
                var json = flowSet.ToCoflowJson(topo);
                string str = JsonConvert.SerializeObject(json);
                using (var sw = new StreamWriter($"10000_3_28.json")) {
                    sw.Write(str);
                }
           // }

        }

        static Flow RandomFlow(Topology topo, int length, double traffic) {
            var f = new List<Switch>();
            var sw = topo.RandomSwitch();
            f.Add(sw);
            while (length-- > 0) {
                var sw1 = sw.RandomLinkedSwitch();
                while (f.Contains(sw1)) {
                    sw1 = sw.RandomLinkedSwitch();
                }
                f.Add(sw1);
            }
            return new Flow(f, traffic);
        }

        static Topology HyperXGen() {
            var topo = new Topology();
            int x = 9;
            for (int i = 0; i < x * x; i++) {
                topo.Switches.Add(new Switch($"Switch{i}"));
            }
            for (int i = 0; i < x * x; i++) {
                var sw1 = topo.Switches[i];
                for (int j = i + 1; j < (i / x) * x + x; j++) {
                    var sw2 = topo.Switches[j];
                    sw1.Link(sw2);
                }
            }
            for (int i = 0; i < x * x; i++) {
                var sw1 = topo.Switches[i];
                for (int j = i + x; j < x * x; j += x) {
                    var sw2 = topo.Switches[j];
                    sw1.Link(sw2);
                }
            }
            return topo;
        }
    }
}