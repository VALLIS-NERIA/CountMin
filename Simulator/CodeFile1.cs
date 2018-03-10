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
using static Simulation.Utils;
using Switch = Simulation.Switch;

//using Tuple1=(System.Double, System.Double);

namespace Simulator {
    using Tup = System.ValueTuple<double, double>;
    using ElemType = Int64;
    static partial class Program {
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


                            using (var sr = new StreamReader($"analysis/analysis_SketchVisor_{(int)(k * 1.2)}_{flow_count}_{topos}.csv")) {
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

        static Task[] FSSTesting() {
            var taskList = new List<Task>();
            var cm = new CountMax(1000, 2);
            var topo = LoadTopo("hyperx9.json");
            cm.Init(topo);
            var flowSet0 = LoadFlow("udp12w_hyperx9_OSPF.json", topo);
            var t0 = DateTime.Now;
            foreach (Flow flow in flowSet0) {
                cm.Update(flow, (ElemType)flow.Traffic);
            }
            var t1 = DateTime.Now;
            var list_cm = new List<Tup>();
            foreach (Flow flow in flowSet0) {
                var query_cm = cm.Query(flow);
                list_cm.Add((flow.Traffic, query_cm));
            }
            var ll_cm = Filter(list_cm, 0.01);
            var count_cm = ll_cm.Count(d => d != 0);
            var t_cm = list_cm.Sum(t => t.Item1);
            Console.WriteLine($"{ll_cm.Average()}  {(t1 - t0).TotalMilliseconds}                                            ");
            foreach (int l in new[] { 1, 2, 3, 4, 5, 6, 7, 10 }) {
                void _do() {
                    var flowSet = LoadFlow("udp12w_hyperx9_OSPF.json", topo);
                    var fss = new FSpaceSaving(1000 * l);
                    var ta = DateTime.Now;
                    foreach (Flow flow in flowSet0) {
                        fss.Update(flow, (ElemType)flow.Traffic);
                    }
                    var tb = DateTime.Now;
                    var list_fss = new List<Tup>();
                    foreach (Flow flow in flowSet0) {
                        var query_fss = fss.Query(flow);
                        list_fss.Add((flow.Traffic, query_fss));
                    }
                    var ll_fss = Filter(list_fss, 0.01);
                    var count_fss = ll_fss.Count(d => d != 0);
                    var t_fss = list_fss.Sum(t => t.Item1);
                    Console.WriteLine($"{l} , {ll_fss.Average()} , {(tb - ta).TotalMilliseconds}                                                    ");
                }
                taskList.Add(new Task(_do));
            }
            return taskList.ToArray();
        }

        static Task[] Prototype() {
            Console.WriteLine("flow,   k,   origin,   cm,   fss,   cs");
            var taskList = new List<Task>();

            foreach (int flow_count in new[] { 1000, 2000, 3000 }) {
                foreach (int k in new[] {/*20,40,60,80,*/100, 150,200, 250,300 }) {
                    void _do() {
                        var topo = LoadTopo("testtopo");
                        var flowSet = LoadFlow($"test_{flow_count}", topo);

                        var cm = new CountMax(k, 2);
                        var fss = new FSpaceSaving(k);
                        var cs = new CountSketch(k, 2);
                        cm.Init(topo);
                        var r_cm = ReRouteWithSketch(topo, flowSet, cm);
                        var r_fss = ReRouteWithSketch(topo, flowSet, fss);
                        var r_cs = ReRouteWithSketch(topo, flowSet, cs);

                        topo.Switches.ForEach(s => s.ClearFlow());
                        //IEnumerable<double> load;

                        flowSet.ForEach(f => f.Assign());
                        var load0 = from sw in topo.FetchLinkLoad() select sw.Value;
                        Console.Write($"{flow_count},   {k},   {load0.Max()}");
                        topo.Switches.ForEach(s => s.ClearFlow());

                        benc(flowSet, r_cm);
                        benc(flowSet, r_fss);
                        benc(flowSet, r_cs);

                        void benc(List<Flow> flowReal, List<Flow> flowR) {
                            topo.Switches.ForEach(s => s.ClearFlow());
                            var iter0 = flowReal.GetEnumerator();
                            var iter = flowR.GetEnumerator();
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
                            Console.Write($",    {load.Max() / load0.Max():F4}");
                        }
                        Console.WriteLine("                      ");
                    }

                    _do();
                    //taskList.Add(new Task(_do));
                }
            }
            return taskList.ToArray();
        }

        static void LoadUDP() {

        }

        static void SVReroute() {
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) {
                        foreach (int k in k_list) {
                            void _do() {
                                var topo = Utils.LoadTopo(topos + ".json");
                                var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                var fout = $"REROUTE_SketchVisor_k{k}_{fin}.json";
                                var flowSet = Utils.LoadFlow(fin, topo);
                                var k_sv = (int)1.2 * k;
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
                                var topo = Utils.LoadTopo(topos + ".json");
                                var flowSet = Utils.LoadFlow(fin, topo);
                                var newFlow = ReRouteWithSketch(topo, flowSet, new CountMax(k));
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
                                    var topo = Utils.LoadTopo(topos + ".json");
                                    var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                    var fout = $"REROUTE_Original_k{k}_{fin}.json";
                                    var flowSet = Utils.LoadFlow(fin, topo);
                                    var flowSet1 = flowSet.OrderByDescending(f => f.Traffic).ToList();
                                    Utils.ReRoute(flowSet1, Greedy.FindPath, k);
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
                            new Task(() =>
                            {
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
            foreach (int k in new[] { 10, 50 }) {
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
    }
}