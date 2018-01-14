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

    static class Program {
        static int[] k_list = {100, 200, 400, 700, 1000, 1500, 2000, 2500, 3000};
        static string[] topo_list = {"fattree8", "hyperx9"};

        static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        private static int[] count_list = {50000, 100000, 200000, 300000};


        static List<Flow> ReRouteWithSketch(string topoJson, string flowJson, ISketch<Flow, ElemType> sketch) {
            var topo = LoadTopo(topoJson);
            var flowSet = LoadFlow(flowJson, topo);
            return ReRouteWithSketch(topo, flowSet, sketch);
        }

        static List<Flow> ReRouteWithSketch(Topology topo, List<Flow> flowSet, ISketch<Flow, ElemType> sketch) {
            foreach (var sw in topo.Switches) {
                sw.ClearFlow();
            }
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
                    //foreach (var flow_count in count_list) 
                        {
                        foreach (int k in new[] {0}.Concat(k_list)) {
                            var topo = LoadTopo(topos + ".json");
                            //var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var fin = $"udp12w_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var fout = $"REROUTE_{name}_k{k}_{fin}.json";
                            var task =
                                new Task(() =>
                                {
                                    try {
                                        var flowReal = LoadFlow(fin, topo);
                                        var flowSet = k != 0 ? LoadFlow(fout, topo) : LoadFlow(fin, topo);
                                        var flow_count = flowSet.Count;
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
                                    }
                                    catch { }
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
                    foreach (var _flow_count in count_list) {
                        foreach (int k in k_list) {
                            var topo = LoadTopo(topos + ".json");
                            //var fin = $"zipf_{_flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var fin = $"udp12w_{topos}_OSPF.json";
                            //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                            var flowSet = LoadFlow(fin, topo);
                            var flow_count = flowSet.Count;
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
            var time = new StreamWriter("timenew.csv");
            var anal = new StreamWriter("analysis_All.csv");
            //anal.WriteLine("topo, k, flow_count, threshold, cm_avg, cm_hit, cm_time, sv_avg, sv_hit, sv_time, cs_avg, cs_hit, cs_time, cm_min, cm_max, sv_min, sv_max, cs_min, cs_max");
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    //foreach (var flow_count in count_list) 
                        {
                        foreach (int k in k_list) {
                            void _do() {
                                var flow_count = "udp12w";
                                var topo = LoadTopo(topos + ".json");
                                var fin = $"{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                                //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                                //var fin = $"udp12w_{topos}_OSPF.json";
                                var flowSet = LoadFlow(fin, topo);
                                //var flow_count = flowSet.Count;
                                var cm = new CountMax(k, 2);
                                cm.Init(topo);
                                int k_sv = (int) (1.2 * k);
                                var sv = new SketchVisor(k_sv);
                                var cs = new CountSketch(k, 2);
                                var fss = new FSpaceSaving(k);
                                //Console.WriteLine($"{topos}, {flow_count}, {k} initing                                    ");

                                var t00 = DateTime.Now;
                                foreach (Flow flow in flowSet) {
                                    cm.Update(flow, (ElemType) flow.Traffic);
                                }
                                var t01 = DateTime.Now;

                                var t10 = DateTime.Now;
                                foreach (Flow flow in flowSet) {
                                    //sv.Update(flow, (ElemType) flow.Traffic);
                                }
                                var t11 = DateTime.Now;

                                var t20 = DateTime.Now;
                                foreach (Flow flow in flowSet) {
                                    cs.Update(flow, (ElemType) flow.Traffic);
                                }
                                var t21 = DateTime.Now;

                                var t30 = DateTime.Now;
                                foreach (Flow flow in flowSet) {
                                    fss.Update(flow, (ElemType) flow.Traffic);
                                }
                                var t31 = DateTime.Now;

                                var t0 = t01 - t00;
                                var t1 = t11 - t10;
                                var t2 = t21 - t20;
                                var t3 = t31 - t30;
                                double cm_time = t0.TotalMilliseconds;
                                double sv_time = t1.TotalMilliseconds;
                                double cs_time = t2.TotalMilliseconds;
                                double fss_time = t3.TotalMilliseconds;
                                //Console.WriteLine($"{topos},{flow_count},{k},{cm_time},{fss_time},{cs_time}");

                                var list_cm = new List<Tup>();
                                var list_sv = new List<Tup>();
                                var list_cs = new List<Tup>();
                                var list_fss = new List<Tup>();
                                //
                                //var data_cm = new StreamWriter($"data/data_CountMax_{k}_{flowSet.Count}_{topos}.csv");
                                //var anal_cm = new StreamWriter($"analysis/analysis_CountMax_{k}_{flowSet.Count}_{topos}.csv");
                                //var data_sv = new StreamWriter($"data/data_SketchVisor_{k_sv}_{flowSet.Count}_{topos}.csv");
                                //var anal_sv = new StreamWriter($"analysis/analysis_SketchVisor_{k_sv}_{flowSet.Count}_{topos}.csv");
                                //var data_cs = new StreamWriter($"data/data_CountSketch_{k}_{flowSet.Count}_{topos}.csv");
                                //var anal_cs = new StreamWriter($"analysis/analysis_CountSketch_{k}_{flowSet.Count}_{topos}.csv");
                                //
                                //
                                //anal_cm.WriteLine("threshold , average , min , max , hits");
                                //anal_sv.WriteLine("threshold , average , min , max , hits");
                                //anal_cs.WriteLine("threshold , average , min , max , hits");
                                //
                                foreach (Flow flow in flowSet) {
                                    var query_cm = cm.Query(flow);
                                    list_cm.Add((flow.Traffic, query_cm));
                                    //data_cm.WriteLine($"{flow.Traffic} , {query_cm}");
                                    //var query_sv = sv.Query(flow);
                                    //list_sv.Add((flow.Traffic, query_sv));
                                    //data_sv.WriteLine($"{flow.Traffic} , {query_sv}");
                                    var query_cs = cs.Query(flow);
                                    list_cs.Add((flow.Traffic, query_cs));
                                    //data_cs.WriteLine($"{flow.Traffic},{query_cs}");
                                    var query_fss = fss.Query(flow);
                                    list_fss.Add((flow.Traffic, query_fss));
                                }
                                //
                                //
                                foreach (var threshold in new[] {0.001,0.0005,0.0001,0.00001}.Reverse()) {
                                    var ll_cm = HHFilter(list_cm,  threshold);
                                    var count_cm = ll_cm.Count(d => d != 0);
                                    var t_cm = list_cm.Sum(t => t.Item1);
                                    //Console.WriteLine($"{threshold} , {ll_cm.Average()} , {ll_cm.Min()} , {ll_cm.Max()} , {count_cm}");
                                    //anal_cm.WriteLine($"{threshold} , {ll_cm.Average()} , {ll_cm.Min()} , {ll_cm.Max()} , {count_cm}");

                                    //var ll_sv = Filter(list_sv, 1 - threshold);
                                    //var count_sv = ll_sv.Count(d => d != 0);
                                    //anal_sv.WriteLine($"{threshold} , {ll_sv.Average()} , {ll_sv.Min()} , {ll_sv.Max()} , {count_sv}");

                                    var ll_cs = HHFilter(list_cs,  threshold);
                                    var count_cs = ll_cs.Count(d => d != 0);
                                    var t_cs = list_cs.Where(t => t.Item2 != 0).Sum(t => t.Item1);
                                    //anal_cs.WriteLine($"{threshold} , {ll_cs.Average()} , {ll_cs.Min()} , {ll_cs.Max()} , {count_cs}");
                                    var ll_fss = HHFilter(list_fss,  threshold);
                                    var count_fss = ll_fss.Count(d => d != 0);
                                    var t_fss = list_fss.Where(t => t.Item2 != 0).Sum(t => t.Item1);

                                    var total = list_cm.Sum(t => t.Item1);
                                    //Console.WriteLine($"\r{topos}, {flow_count}, {k},{threshold},{t_cm/total},{t_fss/total},{t_cs/total},{cm_time},{fss_time},{cs_time}");
                                    Console.WriteLine($"\r{topos}, {flow_count}, {k},{threshold},{ll_cm.Average()},{ll_fss.Average()},{ll_cs.Average()}");
                                    //Console.WriteLine($"\r{topos}, {flow_count}, {k},{threshold} , {ll_fss.Average()} , {ll_fss.Min()} , {ll_fss.Max()} , {count_fss}                                ");
                                    //    //lock (anal) {
                                    //    //    anal.Write($"{topos},{k},{flow_count},{threshold} ,");
                                    //    //    anal.WriteLine(
                                    //    //        $"{ll_cm.Average()},{count_cm},{cm_time},{ll_sv.Average()},{count_sv},{sv_time},{ll_cs.Average()},{count_cs},{cs_time},{ll_cm.Min()} , {ll_cm.Max()} ,{ll_sv.Min()} , {ll_sv.Max()} ,{ll_cs.Min()} , {ll_cs.Max()} ,");
                                    //    //    anal.Flush();
                                    //    //}
                                }
                                //
                                //data_cm.Close();
                                //anal_cm.Close();
                                //data_sv.Close();
                                //anal_sv.Close();
                                //data_cs.Close();
                                //anal_cs.Close();
                                //
                                //lock (time) {
                                //    time.WriteLine($"{topos},{flow_count},{k},{cm_time},{sv_time}");
                                //    time.Flush();
                                //}
                                //Console.WriteLine(
                                //    $"{topos}, {flow_count}, {k} finished in {cm_time}/{sv_time}/{cs_time}, rerouting...                   ");
                                //var fout = $"REROUTE_CountMax_k{k}_{fin}.json";
                                //var newFlow = ReRouteWithSketch(topo, flowSet, cm);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                                //
                                //fout = $"REROUTE_SketchVisor_k{k}_{fin}.json";
                                //newFlow = ReRouteWithSketch(topo, flowSet, sv);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                                //
                                //fout = $"REROUTE_CountSketch_k{k}_{fin}.json";
                                //newFlow = ReRouteWithSketch(topo, flowSet, cs);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                                //var fout = $"REROUTE_FSS_k{k}_{fin}.json";
                                //var newFlow = ReRouteWithSketch(topo, flowSet, fss);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                            }

                            //_do();
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

        static Task[] FSSTesting() {
            var taskList = new List<Task>();
            var cm = new CountMax(1000,2);
            var topo = LoadTopo("hyperx9.json");
            cm.Init(topo);
            var flowSet0 = LoadFlow("udp12w_hyperx9_OSPF.json", topo);
            var t0 = DateTime.Now;
            foreach (Flow flow in flowSet0) {
                cm.Update(flow, (ElemType) flow.Traffic);
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
            Console.WriteLine($"{ll_cm.Average()}  {(t1-t0).TotalMilliseconds}                                            ");
            foreach (int l in new[] {1,2,3,4,5,6,7,10}) {
                void _do() {
                    var flowSet = LoadFlow("udp12w_hyperx9_OSPF.json", topo);
                    var fss = new FSpaceSaving(1000 * l);
                    var ta = DateTime.Now;
                    foreach (Flow flow in flowSet0) {
                        fss.Update(flow, (ElemType) flow.Traffic);
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
                    Console.WriteLine($"{l} , {ll_fss.Average()} , {(tb-ta).TotalMilliseconds}                                                    ");
                }
                taskList.Add(new Task(_do));
            }
            return taskList.ToArray();
        }

        static Task[] Prototype() {
            Console.WriteLine("flow,   k,   origin,   cm,   fss,   cs");
            var taskList = new List<Task>();

            foreach (int flow_count in new[]{1000,2000,3000}) {
                foreach (int k in new[]{/*20,40,60,80,*/100, 200, 300 }) {
                    void _do() {
                        var topo = LoadTopo("testtopo");
                        var flowSet = LoadFlow($"test_{flow_count}", topo);

                        var cm = new CountMax(k, 2);
                        var fss = new FSpaceSaving(k);
                        var cs = new CountSketch(k, 2);

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
                            Console.Write($",    {load.Max()/load0.Max():F4}");
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

        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
#if DEBUG
            Console.WriteLine("--DEBUG--");
#endif

            //var topo = LoadTopo("fattree8" + ".json");
            //var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
            //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
            //var fout = $"fss_reroute.json";
            //var fin = "zipf_50000_fattree8_OSPF.json";
            //var flowReal = LoadFlow(fin, topo);
            //var flowSet =  LoadFlow(fout, topo);
            //double maxLoad = 0;
            //var iter0 = flowReal.GetEnumerator();
            //var iter = flowSet.GetEnumerator();
            //iter.MoveNext();
            //iter0.MoveNext();
            //while (true) {
            //    var flow0 = iter0.Current;
            //    var flow = iter.Current;
            //    flow.Traffic = flow0.Traffic;
            //    flow.Assign();
            //    if (!iter.MoveNext() ||
            //        !iter0.MoveNext()) {
            //        break;
            //    }
            //}
            //var load = from sw in topo.FetchLinkLoad() select sw.Value;
            //iter.Dispose();
            //iter0.Dispose();
            //Console.WriteLine(load.Max());
            //var flowSet =  LoadFlow(fin, topo);
            //var fss = new FSpaceSaving(700);
            //foreach (Flow flow in flowSet) {
            //    fss.Update(flow, (ElemType)flow.Traffic);
            //}
            //var newFlow = ReRouteWithSketch(topo, flowSet, fss);
            //using (var sw = new StreamWriter("fss_reroute.json")) {
            //    sw.Write(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
            //}
            IEnumerable<Task> taskList = new List<Task>();
            //taskList = taskList.Concat(FSSTesting());
            //SketchAppr();
            //taskList = taskList.Concat(CMReroute());
            //SVReroute();
            //taskList = taskList.Concat(SketchCompareAppr());
            taskList = taskList.Concat(Prototype());
            //PartialReroute();
            //taskList = taskList.Concat(BenchMark("Original"));
            //taskList = taskList.Concat(BenchMark("CountMax", false));
            //taskList = taskList.Concat(BenchMark("SketchVisor", false));
            //taskList = taskList.Concat(BenchMark("CountSketch", false));
            //SketchAppr();
            //SketchCompareTime();
            var taskArray = taskList.ToArray();
            RunTask(taskArray);
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


        private static List<double> Filter(IEnumerable<(double, double)> list, double threshold) {
            var list1 = list;
            list1 = list1.OrderByDescending(t => t.Item1);
            IEnumerable<(double, double)> valueTuples = list1 as (double, double)[] ?? list1.ToArray();
            var q = from tuple in valueTuples.Take((int) (threshold * valueTuples.Count())) select (Math.Abs(tuple.Item2 - tuple.Item1) / tuple.Item1);
            return q.ToList();
        }

        private static List<double> HHFilter(IEnumerable<(double, double)> list, double threshold) {
            var fs = new FileStream("tt", FileMode.Append);
            var list1 = list;
            var total = list.Sum(t => t.Item1);
            list1 = list1.OrderByDescending(t => t.Item1);
            IEnumerable<(double, double)> valueTuples = list1 as (double, double)[] ?? list1.ToArray();
            valueTuples = valueTuples.Where(t => t.Item1 > total * threshold);
            var q = from tuple in valueTuples where tuple.Item1 >= total * threshold select (Math.Abs(tuple.Item2 - tuple.Item1) / tuple.Item1);
            return q.ToList();
        }
    }
}