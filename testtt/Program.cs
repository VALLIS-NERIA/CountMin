using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simulation;
using Tup = System.ValueTuple<double, double>;
using ElemType = System.Int64;
using Newtonsoft.Json;


namespace testtt {
    class Program {
        static int[] k_list = {100, 200, 400, 700, 1000, 1500, 2000, 2500, 3000};
        static string[] topo_list = {"fattree8", "hyperx9"};

        static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        static Topology LoadTopo(string fileName)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName)).ToTopology()
                   : JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName + ".json")).ToTopology();

        static List<Flow> LoadFlow(string fileName, Topology topo)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName)).ToCoflow(topo)
                   : JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName + ".json")).ToCoflow(topo);
        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        static int[] count_list =
        {
/*50000, 100000, 200000, */300000
        };
        private static List<double> Filter(List<(double, double)> list, double threshold) {
            //var list1 = (from tuple in list where tuple.Item2 != 0 select tuple).ToList();
            var list1 = list;
            list1.Sort((tuple1, tuple2) => -tuple1.Item1.CompareTo(tuple2.Item1));
            var q = from tuple in list1.Take((int) (threshold * list1.Count)) select tuple.Item2 / tuple.Item1;
            // var count = q.Count(t => t != 0);
            //var q = from tuple in list where tuple.Item1 > threshold select tuple.Item2 / tuple.Item1;

            //var s1 = (from tuple in q select tuple.Item1).Sum();
            //var s2 = (from tuple in q select tuple.Item2).Sum();
            return q.ToList();
        }
            static void Main(string[] args) {
                Directory.SetCurrentDirectory(@"..\..\..\data");
                foreach (string topos in topo_list) {
                    foreach (int k in k_list) {
                        var topo = LoadTopo(topos + ".json");
                        var fin = $"udp12w_{topos}_OSPF.json";
                        var flowSet = LoadFlow(fin, topo);
                        var flow_count = flowSet.Count;
                        var cm = new CountMax<Flow, Switch>.SwitchSketch(k, 2);
                        int k_sv = (int)(1.2 * k);
                        var sv = new SketchVisor.SwitchSketch(k_sv);
                        Console.WriteLine($"{flow_count} in {topos} initing");
                        var t00 = DateTime.Now;
                        foreach (Flow flow in flowSet) {
                            cm.Update(flow, (Int64)flow.Traffic);
                        }
                        var t01 = DateTime.Now;
                        var t10 = DateTime.Now;
                        foreach (Flow flow in flowSet) {
                            sv.Update(flow, (Int64)flow.Traffic);
                        }
                        var t11 = DateTime.Now;
                        var t0 = t01 - t00;
                        var t1 = t11 - t10;
                        Console.WriteLine($"{flow_count} in {topos} finished in {t0.TotalMilliseconds}/{t1.TotalMilliseconds}, rerouting...");

                        var list = new List<Tup>();
                        using (var sw = new StreamWriter($"data/TMPdata_CountMax_{k}_{flowSet.Count}_{topos}.csv")) {
                            foreach (Flow flow in flowSet) {
                                var query = cm.Query(flow);
                                list.Add((flow.Traffic, query));
                                sw.WriteLine($"{flow.Traffic} , {query}");
                            }
                        }

                        using (var sw = new StreamWriter($"analysis/TMPanalysis_CountMax_{k}_{flowSet.Count}_{topos}.csv")) {
                            sw.WriteLine("threshold , average , min , max , hits");
                            foreach (var threshold in new[] { 0.99, 0.98, 0.95 }) {
                                var ll = Filter(list, 1 - threshold);
                                var count = ll.Count(d => d != 0);
                                sw.WriteLine($"{threshold} , {ll.Average()} , {ll.Min()} , {ll.Max()} , {count}");
                            }
                        }

                        var list2 = new List<Tup>();
                        using (var sw = new StreamWriter($"data/TMPdata_SketchVisor_{k_sv}_{flowSet.Count}_{topos}.csv")) {
                            foreach (Flow flow in flowSet) {
                                var query = sv.Query(flow);
                                list2.Add((flow.Traffic, query));
                                sw.WriteLine($"{flow.Traffic} , {query}");
                            }
                        }

                        using (var sw = new StreamWriter($"analysis/TMPanalysis_SketchVisor_{k_sv}_{flowSet.Count}_{topos}.csv")) {
                            sw.WriteLine("threshold , average , min , max , hits");
                            foreach (var threshold in new[] { 0.99, 0.98, 0.95 }) {
                                var ll = Filter(list2, 1 - threshold);
                                var count = ll.Count(d => d != 0);
                                sw.WriteLine($"{threshold} , {ll.Average()} , {ll.Min()} , {ll.Max()} , {count}");
                            }
                        }


                    }
                }
            }
        }
    
}