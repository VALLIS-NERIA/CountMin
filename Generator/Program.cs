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
    public class Program {
        static Random rnd = new Random();
        static Topology LoadTopo(string fileName) => JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName)).ToTopology();
        static List<Flow> LoadFlow(string fileName, Topology topo) => JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName)).ToCoflow(topo);

        static void Main(string[] args) {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            //var topo = HyperXGen(5);
            //var tJ = topo.ToTopologyJson();
            //var json = JsonConvert.SerializeObject(tJ);
            //using (var sw = new StreamWriter("hyperx5.json")) {
            //    sw.Write(json);
            //}
            //foreach (int k in new[] {10, 25, 50, 100, 200, 400, 700, 1000, 2000}) {
                Topology topo = LoadTopo("hyperx5.json");
                var flowSet = LoadFlow($"28_ospf_10w.json", topo);

                ReRoute(flowSet, Greedy.FindPath);



                //var cm = new CountMax<Flow, Switch>(k, 2);
                //foreach (Flow flow in flowSet) {
                //    flow.Assign();
                //    cm.Update(flow, (ulong) flow.Traffic);
                //}
                //foreach (Flow flow in flowSet) {
                //    flow.Traffic = cm.Query(flow);
                //}
                var json = flowSet.ToCoflowJson(topo);
                string str = JsonConvert.SerializeObject(json);
                using (var sw = new StreamWriter($"28_countmax_greedy_10000_{0}.json")) {
                    sw.Write(str);
                }
                Console.WriteLine();
            //}
            ;
            //var flowSet1 = new List<Flow>();
            //var flowSet2 = new List<Flow>();
            //int c = 10000;
            //Random rnd = new Random();
            //while (c-- > 0) {
            //    Flow flow1 = GenerateRoute(topo, OSPF.FindPath, rnd.NextDouble() < 0.2 ? 16 : 1);
            //    //flow1.Assign();
            //    flowSet1.Add(flow1);
            //    Console.Write($"\r{c}");
            //}
            //foreach (Flow flow1 in flowSet1.OrderByDescending(f => f.Traffic)) {
            //    var flow2 = ReRoute(flow1, Greedy.FindPath);
            //    flowSet2.Add(flow2);
            //    flow2.Assign();
            //    Console.Write($"\r{c++}");
            //}

            ////var flowSet = JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText("udp_ospf.json")).ToCoflow(topo);
            ////ReRoute(flowSet, Greedy.FindPath);
            //var json = flowSet1.ToCoflowJson(topo);
            //string str = JsonConvert.SerializeObject(json);
            //using (var sw = new StreamWriter($"28_ospf_10w.json")) {
            //    sw.Write(str);
            //}

            //json = flowSet2.ToCoflowJson(topo);
            //str = JsonConvert.SerializeObject(json);
            //using (var sw = new StreamWriter($"28_greedy_10w.json")) {
            //    sw.Write(str);
            //}


            //foreach (var t in new[] {0.001, 0.01, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1}) {
            //var sr = new StreamReader("udp new.txt");
            //var flowSet = new List<Flow>();
            //while (!sr.EndOfStream) {
            //    double traffic;
            //    try {
            //        traffic = int.Parse(sr.ReadLine());
            //        for (int i = 9; i > 0; i--) {
            //            sr.ReadLine();
            //        }
            //    }
            //    catch {
            //        continue;
            //    }
            //    Flow f = GenerateRoute(topo, OSPF.FindPath, traffic);
            //    while (f == null) {
            //        f = GenerateRoute(topo, OSPF.FindPath, traffic);
            //    }
            //    flowSet.Add(f);
            //}
        }

        public static Flow ReRoute(Flow flow, RoutingAlgorithm algo) {
            var src = flow.IngressSwitch;
            var dst = flow.OutgressSwitch;
            return new Flow(algo(src, dst)) {Traffic = flow.Traffic};
        }

        public static void ReRoute(List<Flow> flowSet, RoutingAlgorithm algo) {
            int i = 1;
            foreach (Flow flow in flowSet) {
                // DO NOT REROUTE BLANK FLOWS
                if (flow.Traffic == 0) {
                    continue;
                }
                var src = flow.IngressSwitch;
                var dst = flow.OutgressSwitch;
                flow.OverrideAssign(algo(src, dst));
                Console.Write($"\r{i++}");
            }
        }

        static Flow GenerateRoute(Topology topo, RoutingAlgorithm algo, double traffic) {
            var src = topo.RandomSwitch();
            var dst = topo.RandomSwitch();
            while (dst == src) {
                dst = topo.RandomSwitch();
            }
            List<Switch> route = algo(src, dst);
            if (route == null) {
                return null;
            }
            Flow f = new Flow(route);
            f.Traffic = traffic;
            return f;
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

        static Topology HyperXGen(int x = 9) {
            var topo = new Topology();
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

        private class Pod {
            public List<Switch> Aggr;
            public List<Switch> Edge;
            public string Name;
            private int K;

            public Pod(string name, int K) {
                this.Name = name;
                this.K = K;
                Aggr = new List<Switch>();
                Edge = new List<Switch>();
                for (int i = 0; i < K / 2; i++) {
                    Aggr.Add(new Switch($"{name} Aggr {i}"));
                    Edge.Add(new Switch($"{name} Edge {i}", true));
                }
                foreach (var sw in Aggr) {
                    foreach (var swe in Edge) {
                        sw.Link(swe);
                    }
                }
            }

            public void CoreLink(IList<Switch> core) {
                int i = 0;
                foreach (var sw in Aggr) {
                    for (int j = 0; j < K / 2; j++) {
                        sw.Link(core[i++]);
                    }
                }
            }

            public IEnumerable<Switch> GetSwitches() { return Aggr.Concat(Edge); }
        }

        static Topology FatTreeGen(int K) {
            if (K % 2 != 0) throw new ArgumentException();
            var n = K / 2;
            var topo = new Topology();
            var core = new List<Switch>();
            var pods = new List<Pod>();
            for (int i = 0; i < n * n; i++) {
                core.Add(new Switch($"Core {i}"));
            }
            for (int i = 0; i < K; i++) {
                var pod = new Pod($"Pod {i}", K);
                pods.Add(pod);
                pod.CoreLink(core);
            }
            var switches = new List<Switch>();
            switches = switches.Concat(core).ToList();
            foreach (var pod in pods) {
                switches = switches.Concat(pod.GetSwitches()).ToList();
            }
            topo.Switches = switches;
            return topo;
        }
    }
}