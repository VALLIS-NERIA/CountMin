using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using Simulation;
using Tup = System.ValueTuple<double, double>;
using static Generator.Program;

//using Tuple1=(System.Double, System.Double);

namespace Simulator {
    using ElemType = Int64;

    class Program {
        static List<Flow> ReRouteWithSketch(string topoJson, string flowJson, ISketch<Flow, ElemType> sketch) {
            var topo = LoadTopo(topoJson);
            var flowSet = LoadFlow(flowJson, topo);
            return ReRouteWithSketch(topo, flowSet, sketch);
        }

        static List<Flow> ReRouteWithSketch(Topology topo, List<Flow> flowSet, ISketch<Flow, ElemType> sketch) {
            foreach (Flow flow in flowSet) {
                sketch.Update(flow, (long) flow.Traffic);
            }
            var newFlow = new List<Flow>();
            foreach (Flow flow in flowSet) {
                newFlow.Add(new Flow(flow) {Traffic = sketch.Query(flow)});
            }
            ReRoute(newFlow, Greedy.FindPath);
            return newFlow;
        }

        static Topology LoadTopo(string fileName)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName)).ToTopology()
                   : JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName + ".json")).ToTopology();

        static List<Flow> LoadFlow(string fileName, Topology topo)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName)).ToCoflow(topo)
                   : JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName + ".json")).ToCoflow(topo);

        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            var k_list = new[] {10, 50, 100, 200, 400, 700, 1000, 2000, 4000, 7000, 10000};
            var topo_list = new[] {"fattree6", "hyperx7"};
            var algo_list = new RoutingAlgorithm[] {OSPF.FindPath /*, Greedy.FindPath*/};
            var count_list = new[] {10000, 20000, 30000, 40000, 50000};
            //var flow_count = 10000;

            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var flowSetIn = LoadFlow(fin + ".json", topo);
                            var task =
                                new Task(() =>
                                {
                                    var cm = new CountMax<Flow, Switch>(k, 2);
                                    var fout = $"REROUTE_CountMax_k{k}_{fin}.json";
                                    Console.WriteLine($"invoking {fin}");
                                    var flowSetOut = ReRouteWithSketch(topo, flowSetIn, cm);
                                    using (var sw = new StreamWriter(fout))
                                        sw.WriteLine(JsonConvert.SerializeObject(flowSetOut.ToCoflowJson(topo)));
                                    Console.WriteLine($"FINISHED {fout}");
                                });
                            taskList.Add(task);
                            task.Start();
                        }
                    }
                }
            }
            Task.WaitAll(taskList.ToArray());
        }

        static void _Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            Console.WriteLine($"{"k",10}{"max",10},{"avg.",10}{"delta",10}");
            foreach (int k in new[] {10, 50}) {
                Topology topo = LoadTopo("fattree6.json");
                var flowReal = LoadFlow("zipf_20000_fattree6_OSPF.json", topo);
                var flowSet = LoadFlow($"REROUTE_CountMax_k{k}_zipf_20000_fattree6_OSPF.json", topo);
                double maxLoad = 0;
                var iter0 = flowReal.GetEnumerator();
                var iter = flowSet.GetEnumerator();
                iter.MoveNext();
                iter0.MoveNext();
                while (true) {
                    var flow0 = iter0.Current;
                    var flow = iter.Current;
                    flow.Traffic = flow0.Traffic;
                    flow0.Assign();
                    if (!iter.MoveNext() ||
                        !iter0.MoveNext()) {
                        break;
                    }
                }
                var load = from sw in topo.FetchLinkLoad() select sw.Value;
                //foreach (var d in topo.Switches) {
                //    Console.WriteLine(d.LinkLoad);
                //}
                Console.WriteLine($"{k,10}{load.Max(),10:F0}{load.Average(),10:F2}{load.StandardDeviation(),10:F2}");
            }
            Console.ReadLine();
        }

        static void Main_(string[] args) {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            Topology topo = JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText("fattree8.json")).ToTopology();
            List<Flow> flowSet = JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText("udp_ospf.json")).ToCoflow(topo);
            var cm = new Simulation.CountMax<Flow, Switch>(flowSet.Count / 200, 2);
            foreach (Flow flow in flowSet) {
                cm.Update(flow, (ElemType) flow.Traffic);
            }

            var list = new List<Tup>();
            using (var sw = new StreamWriter($"data_{flowSet.Count}_{cm.W}.csv")) {
                foreach (Flow flow in flowSet) {
                    var query = cm.Query(flow);
                    list.Add((flow.Traffic, query));
                    sw.WriteLine($"{flow.Traffic} , {query}");
                }
            }

            using (var sw = new StreamWriter($"analysis_{flowSet.Count}_{cm.W}.csv")) {
                //var threshold = 0.9;
                foreach (var threshold in new[] {0.99, 0.9, 0.8, 0.7, 0.5, 0.3}) {
                    //foreach(var t in new[] { 15,0}) {
                    //var threshold = t;
                    var ll = Filter(list, 1 - threshold);
                    sw.WriteLine($"{threshold} , {ll.Average()} , {ll.Min()}");
                }
            }
        }

        private static List<double> Filter(List<(double, double)> list, double threshold) {
            var list1 = (from tuple in list where tuple.Item2 != 0 select tuple).ToList();
            list1.Sort((tuple1, tuple2) => -tuple1.Item1.CompareTo(tuple2.Item1));
            var q = from tuple in list1.Take((int) (threshold * list1.Count)) select tuple.Item2 / tuple.Item1;
            //var q = from tuple in list where tuple.Item1 > threshold select tuple.Item2 / tuple.Item1;

            //var s1 = (from tuple in q select tuple.Item1).Sum();
            //var s2 = (from tuple in q select tuple.Item2).Sum();
            return q.ToList();
        }
    }
}