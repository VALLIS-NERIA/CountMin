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
            //var topo = FatTreeGen(8);
            //var tJ = topo.ToTopologyJson();
            //var json = JsonConvert.SerializeObject(tJ);
            //using (var sw = new StreamWriter("fattree8.json")) {
            //    sw.Write(json);
            //}
            Topology topo = JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText("fattree8.json")).ToTopology();
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

        private class Pod
        {
            public List<Switch> Aggr;
            public List<Switch> Edge;
            public string Name;
            private int K;
            public Pod(string name,int K)
            {
                this.Name = name;
                this.K = K;
                Aggr = new List<Switch>();
                Edge = new List<Switch>();
                for (int i = 0; i <  K / 2; i++) {
                    Aggr.Add(new Switch($"{name} Aggr {i}"));
                    Edge.Add(new Switch($"{name} Edge {i}", true));
                }
                foreach (var sw in Aggr)
                {
                    foreach (var swe in Edge)
                    {
                        sw.Link(swe);
                    }
                }
            }

            public void CoreLink(IList<Switch> core)
            {
                int i = 0;
                foreach (var sw in Aggr )
                {
                    for (int j = 0; j < K/2; j++)
                    {
                        sw.Link(core[i++]);
                    }
                }
            }

            public IEnumerable<Switch> GetSwitches()
            {
                return Aggr.Concat(Edge);
            }
        }

        static Topology FatTreeGen(int K) {
            if (K % 2 != 0) throw new ArgumentException();
            var n = K / 2;
            var topo = new Topology();
            var core = new List<Switch>();
            var pods=new List<Pod>();
            for (int i = 0; i < n * n; i++) {
                core.Add(new Switch($"Core {i}"));
            }
            for (int i = 0; i < K; i++)
            {
                var pod = new Pod($"Pod {i}", K);
                pods.Add(pod);
                pod.CoreLink(core);
            }
            var switches = new List<Switch>();
            switches = switches.Concat(core).ToList();
            foreach (var pod in pods )
            {
                switches = switches.Concat(pod.GetSwitches()).ToList();
            }
            topo.Switches = switches;
            return topo;


        }
    }
}