﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Simulation;
using static Simulation.Utils;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;

namespace Generator {
    public class Program {
        static Random rnd = new Random();
        static int[] k_list = {100, 200, 400, 700, 1000, 1500, 2000, 2500, 3000};
        static string[] topo_list = {"fattree8", "hyperx9"};

        static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        private static int[] count_list = {50000, 100000, 200000, 300000};
        private static Task[] BenchMark(string name = "SketchVisor", bool head = true) {
            if (head) {
                Console.WriteLine($"{"sketch",15}{"topology",10}{"flow_count",10}{"k",10}{"max",15},{"avg.",15}{"delta",15}");
            }
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in new[] { 0 }.Concat(k_list)) {
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var fout = $"REROUTE_{name}_k{k}_{fin}.json";
                            if (!File.Exists(fout)) {
                                continue;
                            }
                            var task =
                                new Task(() =>
                                {
                                    var flowReal = LoadFlow(fin, topo);
                                    var flowSet = k != 0 ? LoadFlow(fout, topo) : LoadFlow(fin, topo);
                                    double maxLoad = 0;
                                    var iter0 = flowReal.GetEnumerator();
                                    var iter = flowSet.GetEnumerator();
                                    iter.MoveNext();
                                    iter0.MoveNext();
                                    while (true) {
                                        var flow0 = iter0.Current;
                                        var flow = iter.Current;
                                        flow.Traffic = flow0.Traffic;
                                        flow.Assign();
                                        if (!iter.MoveNext() ||
                                            !iter0.MoveNext()) {
                                            break;
                                        }
                                    }
                                    var load = from sw in topo.FetchLinkLoad() select sw.Value;
                                    iter.Dispose();
                                    iter0.Dispose();
                                    Console.WriteLine($"{name,15}{topos,10}{flow_count,10}{k,10}{load.Max(),15:F0}{load.Average(),15:F2}{load.StandardDeviation(),15:F2}");
                                });
                            taskList.Add(task);
                        }
                    }
                }
            }
            return taskList.ToArray();
        }
        static void InitGen() {
            var k_list = new[] {10, 25, 50, 100, 200, 400, 700, 1000, 2000};
            var topo_list = new[] {"fattree6", "hyperx7"};
            var algo_list = new RoutingAlgorithm[] {Greedy.FindPath};
            var count_list = new[] {10000, 20000, 30000, 40000, 50000};

            var taskList = new List<Task>();
            foreach (string topos in topo_list) {
                foreach (RoutingAlgorithm algorithm in algo_list) {
                    foreach (var flow_count in count_list) {
                        var topo = LoadTopo(topos + ".json");
                        var task =
                            new Task(() =>
                            {
                                var fold = $"zipf_{flow_count}_{topos}_OSPF.json";
                                var fn = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}.json";
                                var flowOld = LoadFlow(fold, topo);
                                //var traffics =( from flow in flowOld select flow.Traffic).ToList();
                                Console.WriteLine($"invoking {fn}");
                                var flowSet = new List<Flow>();
                                for (int i = 0; i < flow_count; ++i) {
                                    var f = ReRoute(flowOld[i], algorithm);
                                    f.Assign();
                                    flowSet.Add(f);
                                }
                                using (var sw = new StreamWriter(fn))
                                    sw.WriteLine(JsonConvert.SerializeObject(flowSet.ToCoflowJson(topo)));
                                Console.WriteLine($"FINISHED {fn}");
                            });
                        taskList.Add(task);
                        task.Start();
                    }
                }
            }
            Task.WaitAll(taskList.ToArray());
        }


        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            //InitGen();
            var topo = LoadTopo("fattree8.json");
            var floyd = new Floyd(topo).Calc();
            var edges = topo.Switches.Where(sw => sw.IsEdge).ToList();
            var len = edges.Count;
            var flowSet = new List<Flow>();
            var traffics = new List<int>();
            using (var sr = new StreamReader("udp new.txt")) {
                while (!sr.EndOfStream) {
                    traffics.Add(int.Parse(sr.ReadLine()));
                }
            }
            var flowCount = traffics.Count;
            while (flowCount-- > 0) {
                var src = edges[rnd.Next() % len];
                var dst = edges[rnd.Next() % len];
                while (dst == src) {
                    dst = edges[rnd.Next() % len];
                }
                flowSet.Add(new Flow(floyd[src][dst]));
            }
            Console.ReadLine();
        }

        static void MMain(string[] args) {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            //var topo = FatTreeGen(6);
            //var tJ = topo.ToTopologyJson();
            //var json = JsonConvert.SerializeObject(tJ);
            //using (var sw = new StreamWriter("fattree6.json")) {
            //    sw.Write(json);
            //}
            var k_list = new[] {10, 25, 50, 100, 200, 400, 700, 1000, 2000};
            var topo_list = new[] {"fattree8", "hyperx9"};
            var algo_list = new RoutingAlgorithm[] {OSPF.FindPath};
            var count_list = new[] {50000, 100000, 200000, 300000};
            //var flow_count = 10000;

            var taskList = new List<Task>();
            foreach (string topos in topo_list) {
                var topo = LoadTopo(topos + ".json");
                var table = new Floyd(topo).Calc();
                var fn = $"udp12w_{topos}_OSPF.json";
                var traffics = new List<int>();
                using (var sr = new StreamReader("udp new.txt")) {
                    while (!sr.EndOfStream) {
                        traffics.Add(int.Parse(sr.ReadLine()));
                    }
                }
                var flowSet = new List<Flow>();
                foreach (int traffic in traffics) {
                    var src = topo.RandomSwitch();
                    var dst = topo.RandomSwitch();
                    while (dst == src) {
                        dst = topo.RandomSwitch();
                    }
                    var route = table[src][dst];
                    var f = new Flow(route, traffic);
                    flowSet.Add(f);
                }
                using (var sw = new StreamWriter(fn)) {
                    sw.Write(JsonConvert.SerializeObject(flowSet.ToCoflowJson(topo)));
                }
                foreach (RoutingAlgorithm algorithm in algo_list) {
                    foreach (var flow_count in count_list) {
                        //void _do() {
                        //    var flowSet = new List<Flow>();
                        //    var fn = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}.json";
                        //    Console.WriteLine($"invoking {fn}");
                        //    var samples = Zipf.Samples(1, flow_count);
                        //    var traffics = new int[flow_count];
                        //    foreach (int sample in samples) {
                        //        traffics[sample] += 1;
                        //    }
                        //    for (int i = 0; i < flow_count; i++) {
                        //        traffics[i] += 1;
                        //    }
                        //    for (int i = 0; i < flow_count; ++i) {
                        //        var src = topo.RandomSwitch();
                        //        var dst = topo.RandomSwitch();
                        //        while (dst == src) {
                        //            dst = topo.RandomSwitch();
                        //        }
                        //        var route = table[src][dst];
                        //        //traffics.Count(t => t == i);
                        //        var f = new Flow(route, traffics[i]);
                        //        flowSet.Add(f);
                        //        //Console.Write($"\r{i}");
                        //    }
                        //    using (var sw = new StreamWriter(fn))
                        //        sw.WriteLine(JsonConvert.SerializeObject(flowSet.ToCoflowJson(topo)));
                        //    Console.WriteLine($"FINISHED {fn}");
                        //}

                        //var task = new Task(_do);
                        //taskList.Add(task);
                        //task.Start();
                    }
                }
                Task.WaitAll(taskList.ToArray());
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