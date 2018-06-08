using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        internal static int[] k_list = {1000, 1500, 2000, 2500, 3000};
        internal static string[] topo_list = {"Fattree" /*, "HyperX"*/};

        internal static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        //private static int[] count_list = {50000, 100000, 200000, 300000};
        internal static int[] count_list = {50000, 100000, 150000, 200000, 250000, 300000};


        static List<Flow> ReRouteWithSketch(string topoJson, string flowJson, ITopoSketch<Flow, ElemType> sketch) {
            var topo = LoadTopo(topoJson);
            var flowSet = LoadFlow(flowJson, topo);
            return ReRouteWithSketch(topo, flowSet, sketch);
        }

        static List<Flow> ReRouteWithSketch(Topology topo, List<Flow> flowSet, ITopoSketch<Flow, ElemType> sketch) {
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


        static List<ReroutedFlow> ReRouteTopWithSketch(Topology topo, List<Flow> flowSet, ITopoSketch<Flow, ElemType> sketch, double thres) {
            foreach (var sw in topo.Switches) {
                sw.ClearFlow();
            }

            flowSet.Sort((f1, f2) => (int) (f2.Traffic - f1.Traffic));
            foreach (Flow flow in flowSet) {
                sketch.Update(flow, (long) flow.Traffic);
            }

            var newFlow = new List<ReroutedFlow>();
            foreach (Flow flow in flowSet) {
                ReroutedFlow newf = new ReroutedFlow(flow) {Traffic = sketch.Query(flow), OriginTraffic = flow.Traffic};
                newFlow.Add(newf);
            }

            ReRoute(newFlow, Greedy.FindPath, (int) (thres * flowSet.Count));
            return newFlow;
        }


        private delegate Topology TopoFactory();

        static Task[] ConcurrentReroute() {
            void _reroute(object obj) {
                //try
                {
                    var arg = ((IFS obj, string topos, int flow_count, TopoFactory factory)) obj;
                    var topo = arg.factory();
                    var sketch = arg.obj;
                    sketch.Init(topo);
                    var fin = $"{arg.topos}_{arg.flow_count}";
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

                    //foreach (var threshold in new[] {0.01, 0.05}.Reverse()) {
                    //    var ll = RelativeErrorOfTop(list, threshold);
                    //    Console.WriteLine($"\r{topos}, {flow_count}, {k}, {threshold}, {sketch.SketchClassName}, {ll.Average()}, {time}");
                    //}

                    var fout = $"REROUTE_{sketch.SketchClassName}_k{sketch.W}_{fin}.json";
                    var newFlow = ReRouteTopWithSketch(topo, flowSet, sketch, 0.005);
                    using (var sw = new StreamWriter(fout)) {
                        sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToReroutedCoflowJson(topo)));
                    }

                    Console.WriteLine($"\r{arg.topos}, {arg.flow_count}, {sketch.W}");
                }
                //catch (Exception e) {
                //    Console.WriteLine(e.Message);
                //    Console.WriteLine(e.StackTrace);
                //}
            }

            var taskList = new List<Task>();
            var ths = 1000;
            foreach (int k in new[] {1000})
            foreach (string topos in topo_list)
            foreach (var flow_count in count_list) {
                TopoFactory topoF = () => Generator.Program.FatTreeGen(8);
                IFS cm = new FilteredSketch<CountMax.SwitchSketch>(k, 2, ths, () => new CountMax.SwitchSketch(k, 2));
                int k_sv = (int) (1.2 * k);
                IFS sv = new FilteredSketch<SketchVisor.SwitchSketch>(k, 2, ths, () => new SketchVisor.SwitchSketch(k_sv));
                IFS cs = new FilteredSketch<CountSketch.SwitchSketch>(k, 2, ths, () => new CountSketch.SwitchSketch(k, 2));
                IFS fss = new FilteredSketch<FSpaceSaving.SwitchSketch>(k, 2, ths, () => new FSpaceSaving.SwitchSketch(k));
                //_reroute((cm, topos, flow_count, topoF));
                //_reroute((cs, topos, flow_count, topoF));
                //_reroute((fss, topos, flow_count, topoF));
                taskList.Add(new Task(_reroute, (cm, topos, flow_count, topoF)));
                taskList.Add(new Task(_reroute, (cs, topos, flow_count, topoF)));
                taskList.Add(new Task(_reroute, (fss, topos, flow_count, topoF)));
            }


            return taskList.ToArray();
        }


        static Task[] SketchCompareAppr() {
            var taskList = new List<Task>();
            var time = new StreamWriter("timenew.csv");
            var anal = new StreamWriter("analysis_All.csv");
            var ft = Generator.Program.FatTreeGen(8);
            var hy = Generator.Program.HyperXGen(9);
            for (int i = 0; i < hy.Switches.Count; i++) {
                hy.Switches[i].IsEdge = i % 2 == 0;
            }

            var ths = 1000;
            //anal.WriteLine("topo, k, flow_count, threshold, cm_avg, cm_hit, cm_time, sv_avg, sv_hit, sv_time, cs_avg, cs_hit, cs_time, cm_min, cm_max, sv_min, sv_max, cs_min, cs_max");
            //foreach (RoutingAlgorithm algorithm in algo_list)
            foreach (string topos in topo_list)
            foreach (var flow_count in count_list)
            foreach (int k in k_list) {
                //if (k != 1000 /*&& flow_count != 150000 && flow_count!=250000*/) continue;

                void _do() {
                    //var flow_count = "zipf_200000";
                    var topo = topos == "HyperX" ? hy : ft;
                    //var fin = $"zipf_{flow_count}_{topos}_{algorithm.Method.ReflectedType.Name}";
                    var fin = $"{topos}_{flow_count}";
                    //fin = $"REROUTE_CountMax_k{k}_{fin}.json";
                    //var fin = $"udp12w_{topos}_OSPF.json";
                    var flowSet = LoadFlow(fin, topo);
                    //var flow_count = flowSet.Count;
                    var cm = new FilteredSketch<CountMax.SwitchSketch>(k, 2, ths, () => new CountMax.SwitchSketch(k, 2));
                    cm.Init(topo);
                    int k_sv = (int) (1.2 * k);
                    var sv = new FilteredSketch<SketchVisor.SwitchSketch>(k, 2, ths, () => new SketchVisor.SwitchSketch(k_sv));
                    sv.Init(topo);
                    var cs = new FilteredSketch<CountSketch.SwitchSketch>(k, 2, ths, () => new CountSketch.SwitchSketch(k, 2));
                    cs.Init(topo);
                    var fss = new FilteredSketch<FSpaceSaving.SwitchSketch>(k, 2, ths, () => new FSpaceSaving.SwitchSketch(k));
                    fss.Init(topo);
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
                    foreach (var threshold in new[] {0.005, 0.01}.Reverse()) {
                        var ll_cm = RelativeErrorOfTop(list_cm, threshold);
                        var count_cm = ll_cm.Count(d => d != 0);
                        var t_cm = list_cm.Where(t => t.Item2 != 0).Sum(t => t.Item1);
                        //Console.WriteLine($"{threshold} , {ll_cm.Average()} , {ll_cm.Min()} , {ll_cm.Max()} , {count_cm}");
                        //anal_cm.WriteLine($"{threshold} , {ll_cm.Average()} , {ll_cm.Min()} , {ll_cm.Max()} , {count_cm}");

                        //var ll_sv = Filter(list_sv, 1 - threshold);
                        //var count_sv = ll_sv.Count(d => d != 0);
                        //anal_sv.WriteLine($"{threshold} , {ll_sv.Average()} , {ll_sv.Min()} , {ll_sv.Max()} , {count_sv}");

                        var ll_cs = RelativeErrorOfTop(list_cs, threshold);
                        var count_cs = ll_cs.Count(d => d != 0);
                        var t_cs = list_cs.Where(t => t.Item2 != 0).Sum(t => t.Item1);
                        //anal_cs.WriteLine($"{threshold} , {ll_cs.Average()} , {ll_cs.Min()} , {ll_cs.Max()} , {count_cs}");
                        var ll_fss = RelativeErrorOfTop(list_fss, threshold);
                        var count_fss = ll_fss.Count(d => d != 0);
                        var t_fss = list_fss.Where(t => t.Item2 != 0).Sum(t => t.Item1);

                        var total = list_cm.Sum(t => t.Item1);
                        //Console.WriteLine($"\r{topos}, {flow_count}, {k},{threshold},{t_cm/total},{t_fss/total},{t_cs/total},{cm_time},{fss_time},{cs_time}");
                        Console.WriteLine(
                            $"\r{topos}, {flow_count}, {k},{threshold}, {ll_cm.Average()},{ll_fss.Average()},{ll_cs.Average()},{t_cm / total},{t_fss / total},{t_cs / total},{cm_time},{fss_time},{cs_time}");
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
                    //void _reroute(object sketch) {
                    //    try {
                    //        List<Flow> _flowSet = flowSet;
                    //        var _topo = LoadTopo(topos + ".json");
                    //        //lock (flowSet) {
                    //        //    _flowSet = LoadFlow(fin,_topo);
                    //        //}
                    //        var fout = $"REROUTE_CountMax_k{k}_{fin}.json";
                    //        var newFlow = ReRouteWithSketch(_topo, _flowSet, sketch as ITopoSketch<Flow, ElemType>);
                    //        using (var sw = new StreamWriter(fout)) {
                    //            sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToCoflowJson(_topo)));
                    //        }
                    //    }
                    //    catch (Exception e) {
                    //        Console.WriteLine(e.Message);
                    //        Console.WriteLine(e.StackTrace);
                    //    }
                    //}

                    //_reroute(cm);
                    //taskList.Add(new Task(_reroute, cm));
                    //taskList.Add(new Task(_reroute, cs));
                    //taskList.Add(new Task(_reroute, fss));

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


            return taskList.ToArray();
        }

        static void Filter() {
            var topo = global::Generator.Program.FatTreeGen(8);
            var flowSet = LoadFlow("UDP fattree.json", topo);
            int w = 1000;
            int d = 2;
            int count = 5;
            while (count-- > 0) {
                var fcm = new FilteredCountMax(w, d, topo);
                var cm = new CountMax(w, d);
                cm.Init(topo);
                var t00 = DateTime.Now;
                foreach (Flow flow in flowSet) {
                    fcm.Update(flow, (ElemType) flow.Traffic);
                }

                var t = DateTime.Now;
                foreach (Flow flow in flowSet) {
                    cm.Update(flow, (ElemType) flow.Traffic);
                }

                var t11 = DateTime.Now;

                var lf = new List<Tup>();
                var l = new List<Tup>();
                foreach (Flow flow in flowSet) {
                    var qfcm = fcm.Query(flow);
                    var qcm = cm.Query(flow);
                    lf.Add((flow.Traffic, qfcm));
                    l.Add((flow.Traffic, qcm));
                }

                foreach (var threshold in new[] {0.001}.Reverse()) {
                    var llf = RelativeErrorOfTop(lf, threshold);
                    var ll = RelativeErrorOfTop(l, threshold);

                    Console.WriteLine($"\r{w}, {d}, {threshold},{llf.Average()}, {ll.Average()}, {(t - t00).TotalMilliseconds},{(t11 - t).TotalMilliseconds}");
                }
            }
        }

        static void RerouteEval() {
            GC.TryStartNoGCRegion(2L * 1024 * 1024 * 1024);
            foreach (string topos in topo_list)
            foreach (var flow_count in count_list) {
                var topo1 = Generator.Program.FatTreeGen(8);
                var flow1 = LoadFlow($"{topos}_{flow_count}", topo1);
                flow1.ForEach(f => f.Assign());
                var max_orig = topo1.FetchLinkLoad().Max(o => o.Value);
                foreach (int k in k_list) {
                    if (k != 1000 && flow_count != 200000) continue;
                    Console.Write($"{flow_count}, {k}, {max_orig}");
                    foreach (var sketch_str in new[] {nameof(CountMax), nameof(FSpaceSaving), nameof(CountSketch)}) {
                        var topo2 = Generator.Program.FatTreeGen(8);
                        var flow2 = JsonConvert.DeserializeObject<ReroutedCoflowJson>(File.ReadAllText($"REROUTE_{sketch_str}_k{k}_{topos}_{flow_count}.json")).ToCoflow(topo2);
                        flow2.ForEach(f => f.Traffic = f.OriginTraffic);
                        flow2.ForEach(f => f.Assign());
                        var max_reroute = topo2.FetchLinkLoad().Max(o => o.Value);
                        Console.Write($", {max_reroute}");
                    }

                    Console.WriteLine();
                }
            }
        }

        static void TTT() {
            var topo = global::Generator.Program.FatTreeGen(8);
            foreach (int count in count_list) {
                var flowSet = LoadFlow($"Fattree_{count}", topo);
                var traffics = from f in flowSet select f.Traffic;
                var total = traffics.Sum();
                var hhs = from t in traffics where t > (double) total / 10000d select t;
                Console.WriteLine($"{total}, {total / 10000}, {hhs.Count()}");
            }
        }

        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
#if DEBUG
            Debug.WriteLine("--DEBUG--");
#endif
            //Filter();
            //TTT();
            RerouteEval();
            IEnumerable<Task> taskList = new List<Task>();
            //taskList = taskList.Concat(FSSTesting());
            //SketchAppr();
            //taskList = taskList.Concat(CMReroute());
            //SVReroute();
            //taskList = taskList.Concat(SketchCompareAppr());
            //taskList = taskList.Concat(ConcurrentReroute());
            //taskList = taskList.Concat(Prototype());
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


        private static List<double> RelativeErrorOfTop(IEnumerable<(double, double)> list, double threshold) {
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