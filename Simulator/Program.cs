using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using Simulation;
using static Generator.Program;
using Switch = Simulation.Switch;

//using Tuple1=(System.Double, System.Double);

namespace Simulator {
    using Tup = System.ValueTuple<double, double>;
    using ElemType = Int64;

    static class Program {
        static int[] k_list = {100, 200, 400, 700, 1000, 1500, 2000, 2500, 3000};
        static string[] topo_list = {"fattree8", "hyperx9"};

        static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        static int[] count_list = {50000, 100000, 200000, 300000};


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

        static void SVReroute() {
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            void _do() {
                                var topo = LoadTopo(topos + ".json");
                                var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                var fout = $"REROUTE_SketchVisor_k{k}_{fin}.json";
                                var flowSet = LoadFlow(fin, topo);
                                var k_sv = (int) 1.2 * k;
                                var newFlow = ReRouteWithSketch(topo, flowSet, new SketchVisor(k_sv));
                                using (var sw = new StreamWriter(fout)) {
                                    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                }
                                Console.WriteLine(fout);
                            }

                            var task = new Task(_do);
                            task.Start();
                            taskList.Add(task);
                        }
                    }
                }
            }
            Task.WaitAll(taskList.ToArray());
            Console.WriteLine("SVReroute done.");
        }

        static Task[] CMReroute() {
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {


                            void _do() {
                                var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                var fout = $"REROUTE_CountMax_k{k}_{fin}.json";
                                if (File.Exists(fout)) {
                                    Console.WriteLine($"{fout} already exists.");
                                    return;
                                }
                                var topo = LoadTopo(topos + ".json");
                                var flowSet = LoadFlow(fin, topo);
                                var newFlow = ReRouteWithSketch(topo, flowSet, new CountMax<Flow, Switch>(k));
                                using (var sw = new StreamWriter(fout)) {
                                    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                }
                                Console.WriteLine(fout);
                            }

                            var task = new Task(_do);
                            taskList.Add(task);
                        }
                    }
                }
            }
            return taskList.ToArray();
        }

        static void PartialReroute() {
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            void _do() {
                                try {
                                    var topo = LoadTopo(topos + ".json");
                                    var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                    var fout = $"REROUTE_Original_k{k}_{fin}.json";
                                    var flowSet = LoadFlow(fin, topo);
                                    var flowSet1 = flowSet.OrderByDescending(f => f.Traffic).ToList();
                                    ReRoute(flowSet1, Greedy.FindPath, k);
                                    using (var sw = new StreamWriter(fout)) {
                                        sw.WriteLine(JsonConvert.SerializeObject(flowSet1.ToCoflowJson(topo)));
                                    }
                                    Console.WriteLine(fout);
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine(ex.Message);
                                }
                            }

                            //_do();
                            var task = new Task(_do);
                            task.Start();
                            taskList.Add(task);
                        }
                    }
                }
            }
            Task.WaitAll(taskList.ToArray());
        }

        private static Task[] BenchMark(string name = "SketchVisor", bool head = true) {
            if (head) {
                Console.WriteLine($"{"sketch",15}{"topology",10}{"flow_count",10}{"k",10}{"max",15},{"avg.",15}{"delta",15}");
            }
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in new[] {0}.Concat(k_list)) {
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var fout = $"REROUTE_{name}_k{k}_{fin}.json";
                            var flowReal = LoadFlow(fin, topo);
                            var flowSet = k != 0 ? LoadFlow(fout, topo) : LoadFlow(fin, topo);

                            var task =
                                new Task(() => {
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
                                             Console.WriteLine($"{name,15}{topos,10}{flow_count,10}{k,10}{load.Max(),15:F0}{load.Average(),15:F2}{load.StandardDeviation(),15:F2}");
                                         });
                            taskList.Add(task);
                        }
                    }
                }
            }
            return taskList.ToArray();
        }

        static void SketchCompareTime() {
            Console.WriteLine($"{"topology",10}{"flowcount",10}{"k",10}{"sv_k",10},{"CountMax",15}{"SketchVisor",15}");
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                            var flowSet = LoadFlow(fin, topo);
                            var cm = new CountMin(k, 2);
                            var sv = new SketchVisor((int) (1.2 * k));
                            var t00 = DateTime.Now;
                            foreach (Flow flow in flowSet) {
                                cm.Update(flow, (ElemType) flow.Traffic);
                            }
                            var t01 = DateTime.Now;
                            var t10 = DateTime.Now;
                            foreach (Flow flow in flowSet) {
                                sv.Update(flow, (ElemType) flow.Traffic);
                            }
                            var t11 = DateTime.Now;
                            var t0 = t01 - t00;
                            var t1 = t11 - t10;
                            Console.WriteLine($"{topos,10}{flow_count,10}{k,10}{(int) (1.2 * k),10}{t0.TotalMilliseconds,15}{t1.TotalMilliseconds,15}");
                        }
                    }
                }
            }
        }

        static Task[] SketchCompareAppr() {
            var taskList = new List<Task>();
            var time = new StreamWriter("time.csv");
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            void _do() {
                                try {
                                    var topo = LoadTopo(topos + ".json");
                                    var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                    //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                                    var flowSet = LoadFlow(fin, topo);
                                    var cm = new CountMax<Flow, Switch>(k, 2);
                                    int k_sv = (int) (1.2 * k);
                                    var sv = new SketchVisor(k_sv);
                                    Console.WriteLine($"{flow_count} in {topos} initing");
                                    var t00 = DateTime.Now;
                                    foreach (Flow flow in flowSet) {
                                        cm.Update(flow, (ElemType) flow.Traffic);
                                    }
                                    var t01 = DateTime.Now;
                                    var t10 = DateTime.Now;
                                    foreach (Flow flow in flowSet) {
                                        sv.Update(flow, (ElemType) flow.Traffic);
                                    }
                                    var t11 = DateTime.Now;
                                    var t0 = t01 - t00;
                                    var t1 = t11 - t10;
                                    Console.WriteLine(
                                        $"{flow_count} in {topos} finished in {t0.TotalMilliseconds}/{t1.TotalMilliseconds}, rerouting...");


                                    var fout = $"REROUTE_SketchVisor_k{k}_{fin}.json";
                                    var newFlow = ReRouteWithSketch(topo, flowSet, sv);
                                    using (var sw = new StreamWriter(fout)) {
                                        sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                    }


                                    var list = new List<Tup>();

                                    using (var sw = new StreamWriter($"data/data_CountMax_{k}_{flowSet.Count}_{topos}.csv")) {
                                        foreach (Flow flow in flowSet) {
                                            var query = cm.Query(flow);
                                            list.Add((flow.Traffic, query));
                                            sw.WriteLine($"{flow.Traffic} , {query}");
                                        }
                                    }

                                    using (var sw = new StreamWriter($"analysis/analysis_CountMax_{k}_{flowSet.Count}_{topos}.csv")) {
                                        sw.WriteLine("threshold , average , min , max , hits");
                                        foreach (var threshold in new[] {0.99, 0.98, 0.95}) {
                                            var ll = Filter(list, 1 - threshold);
                                            var count = ll.Count(d => d != 0);
                                            sw.WriteLine($"{threshold} , {ll.Average()} , {ll.Min()} , {ll.Max()} , {count}");
                                        }
                                    }

                                    var list2 = new List<Tup>();
                                    using (var sw = new StreamWriter($"data/data_SketchVisor_{k_sv}_{flowSet.Count}_{topos}.csv")) {
                                        foreach (Flow flow in flowSet) {
                                            var query = sv.Query(flow);
                                            list2.Add((flow.Traffic, query));
                                            sw.WriteLine($"{flow.Traffic} , {query}");
                                        }
                                    }

                                    using (var sw = new StreamWriter($"analysis/analysis_SketchVisor_{k_sv}_{flowSet.Count}_{topos}.csv")) {
                                        sw.WriteLine("threshold , average , min , max , hits");
                                        foreach (var threshold in new[] {0.99, 0.98, 0.95}) {
                                            var ll = Filter(list2, 1 - threshold);
                                            var count = ll.Count(d => d != 0);
                                            sw.WriteLine($"{threshold} , {ll.Average()} , {ll.Min()} , {ll.Max()} , {count}");
                                        }
                                    }
                                    lock (time) {
                                        time.WriteLine($"{topos},{flow_count},{k},{t0.TotalMilliseconds},{t1.TotalMilliseconds}");
                                        time.Flush();
                                    }
                                }
                                catch (Exception ex) {
                                    Console.WriteLine(ex.Message);
                                }
                            }

                            var task = new Task(_do);
                            taskList.Add(task);
                        }
                    }
                }
            }
            return taskList.ToArray();
        }

        static void SketchAppr() {
            var taskList = new List<Task>();
            var sw = new StreamWriter("analysisAll.csv");
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                            Console.WriteLine($"{flow_count} in {topos} initing");


                            using (var sr = new StreamReader($"analysis/analysis_CountMax_{k}_{flow_count}_{topos}.csv")) {
                                sr.ReadLine();
                                while (!sr.EndOfStream) {
                                    sw.WriteLine($"{"CountMax"} ,{topos} ,{flow_count} , {k} ,  {sr.ReadLine()}");
                                }
                            }


                            using (var sr = new StreamReader($"analysis/analysis_SketchVisor_{(int) (k * 1.2)}_{flow_count}_{topos}.csv")) {
                                sr.ReadLine();
                                while (!sr.EndOfStream) {
                                    sw.WriteLine($"{"SketchVisor"} ,{topos} ,{flow_count} , {k} ,  {sr.ReadLine()}");
                                }
                            }
                            Console.WriteLine($"-----------{flow_count} in {topos} finished!-----------");


                            //var task = new Task(_do);
                            //task.Start();
                            //taskList.Add(task);
                        }
                    }
                }
            }
            sw.Close();
            //Task.WaitAll(taskList.ToArray());
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
#if DEBUG
            Console.WriteLine("--DEBUG--");
#endif
            IEnumerable<Task> taskList = new List<Task>();
            taskList = taskList.Concat(CMReroute());
            //SVReroute();
            //var taskArray=SketchCompareAppr();
            //PartialReroute();
            //taskList = taskList.Concat(BenchMark("Original"));
            //taskList = taskList.Concat(BenchMark("CountMax", false));
            //taskList = taskList.Concat(BenchMark("SketchVisor", false));
            //SketchAppr();
            //SketchCompareTime();
            var taskArray = taskList.ToArray();
            int i = 0;
            int countOld = 0;
            double wait = 0.5;
            while (i < taskArray.Length) {
                var trd = taskArray.Count(t => t.Status == TaskStatus.Running);
                Console.Write($"\rActive Thread: {trd}, Finished: {i - trd}, Waiting:{taskArray.Length - i}, Speed: {(int) ((Counter - countOld) / wait)}/s.\r");
                countOld = Counter;
                if (trd < 3) {
                    Console.Write("\r");
                    taskArray[i++].Start();
                }
                Thread.Sleep((int) (wait * 1000));
            }
            Task.WaitAll(taskArray);
            Console.WriteLine("Press Q to exit.");
            while (true) {
                var c = Console.ReadKey();
                if (c.Key == ConsoleKey.Q) {
                    Environment.Exit(0);
                }
            }
        }

        private static void BenchmarkGreedy() {
            Console.WriteLine($"{"topology",10}{"flow_count",10}{"max",15},{"avg.",15}{"delta",15}");
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        //foreach (int k in k_list) {
                        var topo = LoadTopo(topos + ".json");
                        var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                        var flowReal = LoadFlow(fin, topo);

                        var task =
                            new Task(() => {
                                         double maxLoad = 0;
                                         foreach (Flow flow in flowReal) {
                                             flow.Assign();
                                         }
                                         var load = from sw in topo.FetchLinkLoad() select sw.Value;
                                         Console.WriteLine($"{topos,10}{flow_count,10}{load.Max(),15:F0}{load.Average(),15:F2}{load.StandardDeviation(),15:F2}");
                                     });
                        taskList.Add(task);
                        task.Start();
                        //}
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
    }
}