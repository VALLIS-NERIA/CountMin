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
        static int[] k_list = {100, 200, 400, 700, 1000, 1500, 2000, 2500, 3000};
        static string[] topo_list = {"fattree8", "hyperx9"};

        static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        //private static int[] count_list = {50000, 100000, 200000, 300000};
        private static int[] count_list = { 150000, 250000};


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



        private static Task[] BenchMark(string name = "SketchVisor", bool head = true) {
            if (head) {
                Console.WriteLine($"{"sketch",15}{"topology",10}{"flow_count",10}{"k",10}{"max",15},{"avg.",15}{"delta",15}");
            }
            var taskList = new List<Task>();
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) 
                        {
                        foreach (int k in new[] {0,1000}) {
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            //var fin = $"udp12w_{topos}_{algorithm.Method.ReflectedType.Name}";
                            var fout = $"REROUTE_{name}_k{k}_{fin}.json";
                            var task =
                                new Task(() =>
                                {
                                    try {
                                        var flowReal = LoadFlow(fin, topo);
                                        var flowSet = k != 0 ? LoadFlow(fout, topo) : LoadFlow(fin, topo);
                                        //var flow_count = flowSet.Count;
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
                            if(k!=1000&&_flow_count!=200000)continue;;
                            var topo = LoadTopo(topos + ".json");
                            var fin = $"zipf_{_flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                            //var fin = $"udp12w_{topos}_OSPF.json";
                            fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                            var flowSet = LoadFlow(fin, topo);
                            var flow_count = flowSet.Count;
                            var cm = new CountMin(k, 2);
                            var sv = new FSpaceSaving((int) (1 * k));
                            var cs = new CountSketch((int) (1 * k),2);
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
                            var t20 = DateTime.Now;
                            foreach (Flow flow in flowSet) {
                                cs.Update(flow, (ElemType) flow.Traffic);
                            }
                            var t21 = DateTime.Now;
                            var t0 = t01 - t00;
                            var t1 = t11 - t10;
                            var t2 = t21 - t20;
                            Console.WriteLine($"{topos,10},{flow_count,10},{k,10},{t0.TotalMilliseconds,15},{t1.TotalMilliseconds,15},{t2.TotalMilliseconds,15}");
                        }
                    }
                }
            }
        }
        
        static Task[] ConcurrentReroute() {
            var taskList = new List<Task>();
            var k = 1000;
            foreach (string topos in topo_list) {
                foreach (var flow_count in count_list) {
                    void _reroute(object obj) {
                        try {
                            ISketch<Flow, ElemType> sketch = (ISketch<Flow, long>) obj;
                            var topo = LoadTopo(topos + ".json");
                            (sketch as CountMax)?.Init(topo);
                            var fin = $"zipf_{flow_count}_{topos}_OSPF";
                            var flowSet = LoadFlow(fin, topo);
                            var t00 = DateTime.Now;
                            foreach (Flow flow in flowSet) {
                                sketch.Update(flow, (ElemType) flow.Traffic);
                            }
                            var t01 = DateTime.Now;
                            double time = (t01 - t00).TotalMilliseconds;
                            var list = new List<Tup>();
                            foreach (Flow flow in flowSet) {
                                var query_cm = sketch.Query(flow);
                                list.Add((flow.Traffic, query_cm));
                            }
                            foreach (var threshold in new[] {0.01, 0.05}.Reverse()) {
                                var ll = Filter(list, threshold);
                                Console.WriteLine($"\r{topos}, {flow_count}, {k}, {threshold}, {obj.GetType().Name}, {ll.Average()}, {time}");
                            }
                            var fout = $"REROUTE_{obj.GetType().Name}_k{k}_{fin}.json";
                            var newFlow = ReRouteWithSketch(topo, flowSet, sketch);
                            using (var sw = new StreamWriter(fout)) {
                                sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                            }
                        }
                        catch (Exception e) {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    }

                    var cm = new CountMax(k, 2);
                    var cs = new CountSketch(k, 2);
                    var fss = new FSpaceSaving(k);

                    taskList.Add(new Task(_reroute, cm));
                    taskList.Add(new Task(_reroute, cs));
                    taskList.Add(new Task(_reroute, fss));
                }
            }
            return taskList.ToArray();
        }


        static Task[] SketchCompareAppr() {
            var taskList = new List<Task>();
            var time = new StreamWriter("timenew.csv");
            var anal = new StreamWriter("analysis_All.csv");
            //anal.WriteLine("topo, k, flow_count, threshold, cm_avg, cm_hit, cm_time, sv_avg, sv_hit, sv_time, cs_avg, cs_hit, cs_time, cm_min, cm_max, sv_min, sv_max, cs_min, cs_max");
            foreach (RoutingAlgorithm algorithm in algo_list) {
                foreach (string topos in topo_list) {
                    foreach (var flow_count in count_list) 
                        {
                        foreach (int k in k_list) {
                            if (k != 1000 /*&& flow_count != 150000 && flow_count!=250000*/) continue;
                            void _do() {
                                //var flow_count = "zipf_200000";
                                var topo = LoadTopo(topos + ".json");
                                var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
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
                                foreach (var threshold in new[] {0.01,0.05}.Reverse()) {
                                    var ll_cm = Filter(list_cm,  threshold);
                                    var count_cm = ll_cm.Count(d => d != 0);
                                    var t_cm = list_cm.Sum(t => t.Item1);
                                    //Console.WriteLine($"{threshold} , {ll_cm.Average()} , {ll_cm.Min()} , {ll_cm.Max()} , {count_cm}");
                                    //anal_cm.WriteLine($"{threshold} , {ll_cm.Average()} , {ll_cm.Min()} , {ll_cm.Max()} , {count_cm}");

                                    //var ll_sv = Filter(list_sv, 1 - threshold);
                                    //var count_sv = ll_sv.Count(d => d != 0);
                                    //anal_sv.WriteLine($"{threshold} , {ll_sv.Average()} , {ll_sv.Min()} , {ll_sv.Max()} , {count_sv}");

                                    var ll_cs = Filter(list_cs,  threshold);
                                    var count_cs = ll_cs.Count(d => d != 0);
                                    var t_cs = list_cs.Where(t => t.Item2 != 0).Sum(t => t.Item1);
                                    //anal_cs.WriteLine($"{threshold} , {ll_cs.Average()} , {ll_cs.Min()} , {ll_cs.Max()} , {count_cs}");
                                    var ll_fss = Filter(list_fss,  threshold);
                                    var count_fss = ll_fss.Count(d => d != 0);
                                    var t_fss = list_fss.Where(t => t.Item2 != 0).Sum(t => t.Item1);

                                    var total = list_cm.Sum(t => t.Item1);
                                    //Console.WriteLine($"\r{topos}, {flow_count}, {k},{threshold},{t_cm/total},{t_fss/total},{t_cs/total},{cm_time},{fss_time},{cs_time}");
                                    Console.WriteLine($"\r{topos}, {flow_count}, {k},{ll_cm.Average()},{ll_fss.Average()},{ll_cs.Average()},{cm_time},{fss_time},{cs_time}");
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
                                void _reroute(object sketch) {
                                    try {
                                        List<Flow> _flowSet = flowSet;
                                        var _topo = LoadTopo(topos + ".json");
                                        //lock (flowSet) {
                                        //    _flowSet = LoadFlow(fin,_topo);
                                        //}
                                        var fout = $"REROUTE_CountMax_k{k}_{fin}.json";
                                        var newFlow = ReRouteWithSketch(_topo, _flowSet, sketch as ISketch<Flow, ElemType>);
                                        using (var sw = new StreamWriter(fout)) {
                                            sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(_topo)));
                                        }
                                    }
                                    catch(Exception e) {
                                        Console.WriteLine(e.Message);
                                        Console.WriteLine(e.StackTrace);
                                    }
                                }
                                //_reroute(cm);
                                taskList.Add(new Task(_reroute, cm));
                                taskList.Add(new Task(_reroute, cs));
                                taskList.Add(new Task(_reroute, fss));
                                
                                //fout = $"REROUTE_SketchVisor_k{k}_{fin}.json";
                                //newFlow = ReRouteWithSketch(topo, flowSet, sv);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                                
                                //fout = $"REROUTE_CountSketch_k{k}_{fin}.json";
                                //newFlow = ReRouteWithSketch(topo, flowSet, cs);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                                //fout = $"REROUTE_FSS_k{k}_{fin}.json";
                                //newFlow = ReRouteWithSketch(topo, flowSet, fss);
                                //using (var sw = new StreamWriter(fout)) {
                                //    sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(topo)));
                                //}
                            }

                            _do();

                            //_do();
                            //var task = new Task(_do);
                            //taskList.Add(task);
                        }
                    }
                }
            }
            return taskList.ToArray();
        }


        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
#if DEBUG
            Console.WriteLine("--DEBUG--");
#endif

            IEnumerable<Task> taskList = new List<Task>();
            //taskList = taskList.Concat(FSSTesting());
            //SketchAppr();
            //taskList = taskList.Concat(CMReroute());
            //SVReroute();
            //taskList = taskList.Concat(SketchCompareAppr());
            //taskList = taskList.Concat(ConcurrentReroute());
            taskList = taskList.Concat(Prototype());
            //PartialReroute();
            //taskList = taskList.Concat(BenchMark("Original"));
            //taskList = taskList.Concat(BenchMark("CountMax", false));
            //taskList = taskList.Concat(BenchMark(nameof(FSpaceSaving), false));
            //taskList = taskList.Concat(BenchMark("CountSketch", false));
            //SketchAppr();
            //SketchCompareTime();
            var taskArray = taskList.ToArray();
            RunTask(taskArray);
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