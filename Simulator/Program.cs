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
    using CountMaxSketch = Simulation.CountMaxSketch.Sketch;

    static partial class Program {
        static Random rnd = new Random();
        internal static int[] k_list = {1000, 1500, 2000, 2500, 3000};
        internal static int[] k_proto = {100, 200, 300};
        internal static int[] flow_proto = {1000, 2000, 3000};

        internal static string[] topo_list =
        {
            "Fattree",
            "Spine",
            "SpineNew"
            /*"HyperX",*/
        };

        internal static RoutingAlgorithm[] algo_list = {OSPF.FindPath /*, Greedy.FindPath*/};

        //static int[] count_list = {10000, 20000, 30000, 40000, 50000};
        //private static int[] count_list = {50000, 100000, 200000, 300000};
        internal static int[] count_list = {50000, 100000, 150000, 200000, 250000, 300000};

        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
#if DEBUG
            Debug.WriteLine("--DEBUG--");
#endif
            //HalfHalfTest();
            //Filter();
            //TTT();
            IEnumerable<Task> taskList = new List<Task>();
            var test = new {Key = "test", Value = "test"};

            //taskList = taskList.Concat(FSSTesting());
            //SketchAppr();
            //taskList = taskList.Concat(CMReroute());
            //SVReroute();
            //taskList = taskList.Concat(SketchCompareAppr());
            taskList = taskList.Concat(SketchCompare());
            //taskList = taskList.Concat(ProtoNew());
            //taskList = taskList.Concat(ConcurrentReroute());
            //taskList = taskList.Concat(Prototype());
            //PartialReroute();
            //taskList = taskList.Concat(BenchMark("Original"));
            //taskList = taskList.Concat(BenchMark("CountMax", false));
            //taskList = taskList.Concat(BenchMark(nameof(FSpaceSaving), false));
            //taskList = taskList.Concat(BenchMark("CountSketch", false));
            //SketchAppr();
            //SketchCompareTime();
            //HalfHalfTest();
            var taskArray = taskList.ToArray();
            RunTask(taskArray, 3, false);
            //PrintToTxt();
            //var oldOut = Console.Out;
            //var sw = new StreamWriter("dddddddaaaata.txt");
            //Console.SetOut(sw);
            //RerouteEvalProto();
            //sw.Flush();
            //Console.SetOut(oldOut);
            //sw.Close();
            Console.WriteLine("Finished.");
            Console.ReadLine();
        }

        private delegate Topology TopoFactory();

        static Task[] ConcurrentReroute() {


            var taskList = new List<Task>();
            var ths = 1000;
            foreach (int k in k_list)
            foreach (string topos in topo_list)
            foreach (var flow_count in count_list) {
                if (k != 1000 && flow_count != 200000) continue;
                //TopoFactory topoF = topos.StartsWith("Spine") ? (() => Generator.Program.LeafSpineGen() ):( () => Generator.Program.FatTreeGen());
                TopoFactory topoF = topos.StartsWith("Spine") ? (TopoFactory) (() => Generator.Program.LeafSpineGen()) : () => Generator.Program.FatTreeGen();
                IFS cm = new HalfSketch<CountMax.SwitchSketch>(k, 2, ths, () => new CountMax.SwitchSketch(k, 2));
                int k_sv = (int) (1.2 * k);
                IFS sv = new HalfSketch<SketchVisor.SwitchSketch>(k, 2, ths, () => new SketchVisor.SwitchSketch(k_sv));
                IFS cs = new HalfSketch<CountSketch.SwitchSketch>(k, 2, ths, () => new CountSketch.SwitchSketch(k, 2));
                IFS fss = new HalfSketch<FSpaceSaving.SwitchSketch>(k, 2, ths, () => new FSpaceSaving.SwitchSketch(k));
                //_reroute((cm, topos, flow_count, topoF));
                //_reroute((cs, topos, flow_count, topoF));
                //_reroute((fss, topos, flow_count, topoF));
                taskList.Add(new Task(Reroute, (cm, topos, flow_count, topoF)));
                taskList.Add(new Task(Reroute, (cs, topos, flow_count, topoF)));
                taskList.Add(new Task(Reroute, (fss, topos, flow_count, topoF)));
            }


            return taskList.ToArray();
        }

        static Task[] SketchCompare() {
            var taskList = new List<Task>();
            var ft = Generator.Program.LeafSpineGen();
            var flow_count = 200000;
            var topos = "SpineNew";
            //var k = 1000;
            //var ths_list = new []{0, 200, 400, 600, 800, 1000, 1200, 1400, 1600, 1800, 2000 };
            var d_list = Enumerable.Range(1, 10);
            //for (var _ths = 0; _ths <= 3000; _ths += 200)
            var d = 2;
            //foreach (int flow_count in count_list) 
                    
            
            foreach (int k in k_list) 
            //var _ths = 1000;
            //foreach (int d in d_list) 
            {

                void _do(object obj) {
                    var ths = 0;
                    //var topo = Generator.Program.FatTreeGen();
                    var topo = ft;
                    var fin = $"{topos}_{flow_count}";
                    var finRef = $"zipf_{flow_count}_fattree8_OSPF";
                    var flowSet = LoadFlow(fin, topo);
                    //flowSet.Sort((f1, f2) => rnd.NextDouble() < 0.5 ? -1 : 1);
                    //flowSet.Sort((f1,f2)=>(int) (f2.Traffic-f1.Traffic));
                    //var flowSetRef = LoadFlow(finRef, Generator.Program.FatTreeGen());
                    var flowSetRef = flowSet.Select(f => f.Traffic).ToArray();
                    var cm = (IFS) obj;
                    var eg = new EgressSketch<CountMax.SwitchSketch>(cm.W, 2, 0, () => new CountMax.SwitchSketch(cm.W, d));
                    cm.Init(topo);
                    eg.Init(topo);
                    var edges = topo.Switches.Where(sw => sw.IsEdge).ToList();
                    foreach (Flow flow in flowSet) {
                        var x1 = edges.IndexOf(flow.IngressSwitch);
                        var x2 = edges.IndexOf(flow.OutgressSwitch);
                                flow.Nodes[0] = edges[x1 / 2];
                                flow.Nodes[2] = edges[x2 / 2 + edges.Count/2];
                    }

                    for (int i = 0; i < flowSet.Count; i++) {
                        Flow flow = flowSet[i];
                        //flow.Traffic = flowSetRef[i / 50];
                        //Flow flowRef = flowSetRef[i];
                        //flow.Traffic = flow.Traffic * (Math.Pow(Math.Min((i - 100), 0), 2) + 1);
                        cm.Update(flow, (ElemType) flow.Traffic);
                        eg.Update(flow, (ElemType) flow.Traffic);
                    }

                    var list_cm = new List<Tup>();
                    var list_eg = new List<Tup>();
                    foreach (Flow flow in flowSet) {
                        var query_cm = cm.Query(flow);
                        list_cm.Add((flow.Traffic, query_cm));
                        var query_eg = eg.Query(flow);
                        list_eg.Add((flow.Traffic, query_eg));
                    }
                    string buf = k.ToString();
                    foreach (var threshold in new[] {0.005, 0.01}.Reverse()) {
                        var ll_cm = RelativeErrorOfTop(list_cm, threshold);
                        var ll_eg = RelativeErrorOfTop(list_eg, threshold);
                        buf += $",{ll_cm.Average()},{ll_eg.Average()}";
                        //buf += $",{HHFilter(list_cm, 1d / 10000).Average()},{HHFilter(list_eg, 1d / 10000).Average()}";
                        //Console.WriteLine($"\r{topos},{cm.GetType().Name}, {k}, {d}, {threshold},{ll_cm.Average()}");
                    }
                    buf = buf.PadRight(Console.WindowWidth - 1);
                    Console.WriteLine(buf);
                }

                //_do(new HalfSketch<CountMax.SwitchSketch>(k, 2, 0, () => new CountMax.SwitchSketch(k, d)));

                //_do();
                var task = new Task(_do, new HalfSketch<CountMax.SwitchSketch>( k, 2, 0, () => new CountMax.SwitchSketch((int) (k), d)));
                //var task = new Task(_do, new EgressSketch<CountMax.SwitchSketch>( k, 2, 0, () => new CountMax.SwitchSketch((int) (k*2), d)));
                //var task1 = new Task(_do, new EgressSketch<CountMax.SwitchSketch>(2 * k, 2, 0, () => new CountMax.SwitchSketch(k, d)));
                taskList.Add(task);
                //taskList.Add(task1);
            }


            return taskList.ToArray();
        }

        static void HalfHalfTest() {
            var flowSet = LoadFlow("Fattree_50000", Generator.Program.FatTreeGen());
            CountMaxSketch s1 = new CountMaxSketch(2000, 2);
            CountMaxSketch s2 = new CountMaxSketch(2000, 2);
            CountMaxSketch s3 = new CountMaxSketch(2000, 4);
            foreach (Flow flow in flowSet) {
                if (flow.GetHashCode() % 2 == 0) {
                    s1.Update(flow, (ElemType) flow.Traffic);
                }
                else {
                    s2.Update(flow, (ElemType) flow.Traffic);
                }

                s3.Update(flow, (ElemType) flow.Traffic);
            }

            //data_cm.Close();
            //
            //
            foreach (var threshold in new[] {0.005, 0.01}.Reverse()) {
                var ll1 = RelativeError2(flowSet, threshold, s1, s2);
                var ll3 = RelativeError2(flowSet, threshold, s3);
                //Console.WriteLine($"\r{topos}, {flow_count}, {k},{threshold},{t_cm/total},{t_fss/total},{t_cs/total},{cm_time},{fss_time},{cs_time}");
                Console.WriteLine(
                    $"\r{threshold}, {ll1.Average()}, {ll3.Average()}                              ");
            }
        }

        static Task[] ProtoNew() {

            var taskList = new List<Task>();
            var topos = "Spine";
            foreach (int k in k_proto)
            foreach (int flow_count in flow_proto) {
                IFS cm = new EgressSketch<CountMax.SwitchSketch>(k, 2, 0, () => new CountMax.SwitchSketch(k, 2));
                IFS cs = new EgressSketch<CountSketch.SwitchSketch>(k, 2, 0, () => new CountSketch.SwitchSketch(k, 2));
                IFS fss = new EgressSketch<FSpaceSaving.SwitchSketch>(k, 2, 0, () => new FSpaceSaving.SwitchSketch(k));
                TopoFactory topoF = Generator.Program.TestTopoGen;
                taskList.Add(new Task(Reroute, (cm, topos, flow_count, topoF)));
                taskList.Add(new Task(Reroute, (cs, topos, flow_count, topoF)));
                taskList.Add(new Task(Reroute, (fss, topos, flow_count, topoF)));

            }
            return taskList.ToArray();
        }

        private static void Reroute(object obj) {
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
            var newFlow = arg.topos.StartsWith("Spine")
                              ? ReRouteTopWithSketch(topo, flowSet, sketch, 0.01, GreedySpine.FindPath)
                              : ReRouteTopWithSketch(topo, flowSet, sketch, 0.01);
            using (var sw = new StreamWriter(fout)) {
                sw.WriteLine(JsonConvert.SerializeObject(newFlow.ToReroutedCoflowJson(topo)));
            }

            Console.WriteLine($"\r{arg.topos}, {arg.flow_count}, {sketch.W}");


        }

        static void RerouteEvalProto() {
            var topos = "Spine";
            foreach (var flow_count in flow_proto) {
                var topo1 = Generator.Program.TestTopoGen();
                var flow1 = LoadFlow($"{topos}_{flow_count}", topo1);
                flow1.ForEach(f => f.Assign());
                var max_orig = topo1.FetchLinkLoad().Max(o => o.Value);
                foreach (int k in k_proto) {
                    if (k != 100 && flow_count != 2000) continue;
                    Console.Write($"{flow_count}, {k}, {max_orig}");
                    foreach (var sketch_str in new[] {nameof(CountMax), nameof(FSpaceSaving), nameof(CountSketch)}) {
                        var topo2 = Generator.Program.TestTopoGen();
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

        static List<double> RelativeError2(List<Flow> flowSet, double threshold, params ISketch<ElemType>[] sketch) {
            flowSet.Sort((f1, f2) => (int) (f2.Traffic - f1.Traffic));
            var top = flowSet.Take((int) (threshold * flowSet.Count));
            var q = new List<double>();
            foreach (Flow f in top) {
                ElemType esti = 0;
                foreach (var s in sketch) {
                    var res = s.Query(f);
                    if (res > esti) esti = res;
                }

                q.Add(Math.Abs((esti - f.Traffic) / f.Traffic));
            }

            return q;
        }

        struct HHData {
            public double hh_cm1;
            public double hh_cm2;
            public double hh_cm10;
            public double hh_fss1;
            public double hh_fss2;
            public double hh_fss10;
            public double hh_cs1;
            public double hh_cs2;
            public double hh_cs10;
        }

        struct DataLine {
            public string topos;
            public int flow_count;
            public int k;
            public double threshold;
            public double cm_err;
            public double fss_err;
            public double cs_err;
            public double cm_hit;
            public double fss_hit;
            public double cs_hit;
            public double cm_time;
            public double fss_time;
            public double cs_time;
            public HHData hh;
        }

        static List<DataLine> data = new List<DataLine>();

        static Task[] SketchCompareAppr() {
            var taskList = new List<Task>();
            var ft = Generator.Program.FatTreeGen(8);
            var sp = Generator.Program.LeafSpineGen();

            var ths = 200;
            //anal.WriteLine("topo, k, flow_count, threshold, cm_avg, cm_hit, cm_time, sv_avg, sv_hit, sv_time, cs_avg, cs_hit, cs_time, cm_min, cm_max, sv_min, sv_max, cs_min, cs_max");
            //foreach (RoutingAlgorithm algorithm in algo_list)
            foreach (string topos in topo_list)
            foreach (var flow_count in count_list)
            foreach (int k in k_list) {
                //if (k != 1000 /*&& flow_count != 150000 && flow_count!=250000*/) continue;
                if (k != 1000 && flow_count != 200000) continue;

                void _do() {
                    var topo = topos.StartsWith("Spine") ? sp : ft;
                    var fin = $"{topos}_{flow_count}";
                    var flowSet = LoadFlow(fin, topo);
                    //var flow_count = flowSet.Count;
                    var cm = new HalfSketch<CountMax.SwitchSketch>(k, 2, ths, () => new CountMax.SwitchSketch(k, 2));
                    cm.Init(topo);
                    int k_sv = (int) (1.2 * k);
                    var sv = new HalfSketch<SketchVisor.SwitchSketch>(k, 2, ths, () => new SketchVisor.SwitchSketch(k_sv));
                    sv.Init(topo);
                    var cs = new HalfSketch<CountSketch.SwitchSketch>(k, 2, ths, () => new CountSketch.SwitchSketch(k, 2));
                    cs.Init(topo);
                    var fss = new HalfSketch<FSpaceSaving.SwitchSketch>(k, 2, ths, () => new FSpaceSaving.SwitchSketch(k));
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

                    foreach (Flow flow in flowSet) {
                        var query_cm = cm.Query(flow);
                        list_cm.Add((flow.Traffic, query_cm));
                        var query_cs = cs.Query(flow);
                        list_cs.Add((flow.Traffic, query_cs));
                        var query_fss = fss.Query(flow);
                        list_fss.Add((flow.Traffic, query_fss));
                    }


                    var hh_cm1 = HHFilter(list_cm, 1d / 1000).Average();
                    var hh_cm2 = HHFilter(list_cm, 1d / 2000).Average();
                    var hh_cm10 = HHFilter(list_cm, 1d / 10000).Average();
                    var hh_fss1 = HHFilter(list_fss, 1d / 1000).Average();
                    var hh_fss2 = HHFilter(list_fss, 1d / 2000).Average();
                    var hh_fss10 = HHFilter(list_fss, 1d / 10000).Average();
                    var hh_cs1 = HHFilter(list_cs, 1d / 1000).Average();
                    var hh_cs2 = HHFilter(list_cs, 1d / 2000).Average();
                    var hh_cs10 = HHFilter(list_cs, 1d / 10000).Average();
                    //data_cm.Close();
                    //
                    //
                    foreach (var threshold in new[] {0.005, 0.01}.Reverse()) {
                        var ll_cm = RelativeErrorOfTop(list_cm, threshold);
                        var ll_cs = RelativeErrorOfTop(list_cs, threshold);
                        var ll_fss = RelativeErrorOfTop(list_fss, threshold);
                        var t_cm = ElephantCover(list_cm, threshold);
                        var t_fss = ElephantCover(list_fss, threshold);
                        var t_cs = ElephantCover(list_cs, threshold);
                        Console.WriteLine(
                            $"\r{topos}, {flow_count}, {k},{threshold}, {ll_cm.Average()},{ll_fss.Average()},{ll_cs.Average()},{t_cm},{t_fss},{t_cs},{cm_time},{fss_time},{cs_time}");
                        lock (data) {
                            data.Add(new DataLine
                            {
                                topos = topos,
                                flow_count = flow_count,
                                k = k,
                                threshold = threshold,
                                cm_err = ll_cm.Average(),
                                fss_err = ll_fss.Average(),
                                cs_err = ll_cs.Average(),
                                cm_hit = t_cm,
                                fss_hit = t_fss,
                                cs_hit = t_cs,
                                cm_time = cm_time,
                                cs_time = cs_time,
                                fss_time = fss_time,
                                hh = new HHData
                                {
                                    hh_cm1 = hh_cm1,
                                    hh_cm2 = hh_cm2,
                                    hh_cm10 = hh_cm10,
                                    hh_fss1 = hh_fss1,
                                    hh_fss2 = hh_fss2,
                                    hh_fss10 = hh_fss10,
                                    hh_cs1 = hh_cs1,
                                    hh_cs2 = hh_cs2,
                                    hh_cs10 = hh_cs10,
                                }
                            });
                        }

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

        static void RerouteEval() {
            foreach (string topos in topo_list)
            foreach (var flow_count in count_list) {
                var topo1 = topos == "Fattree" ? Generator.Program.FatTreeGen(8) : Generator.Program.LeafSpineGen();
                var flow1 = LoadFlow($"{topos}_{flow_count}", topo1);
                flow1.ForEach(f => f.Assign());
                var max_orig = topo1.FetchLinkLoad().Max(o => o.Value);
                foreach (int k in k_list) {
                    if (k != 1000 && flow_count != 200000) continue;
                    Console.Write($"{flow_count}, {k}, {max_orig}");
                    foreach (var sketch_str in new[] {nameof(CountMax), nameof(FSpaceSaving), nameof(CountSketch)}) {
                        var topo2 = topos == "Fattree" ? Generator.Program.FatTreeGen(8) : Generator.Program.LeafSpineGen();
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

        static void PrintToTxt() {
            // approximate
            foreach (string topos in topo_list) {
                string topo_prefix = topos == "Fattree" ? "ft" : "hy";
                var q = from d in data where d.topos == topos && d.k == 1000 orderby d.flow_count ascending select d;
                // ft_flow_appr_1000_095
                using (var sw = new StreamWriter(topo_prefix + "_flow_appr_1000_095.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.flow_count}\t{d.cm_err}\t{d.fss_err}\t{d.cs_err}");


                using (var sw = new StreamWriter(topo_prefix + "_flow_appr_1000_099.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.01)
                            sw.WriteLine($"{d.flow_count}\t{d.cm_err}\t{d.fss_err}\t{d.cs_err}");


                using (var sw = new StreamWriter(topo_prefix + "_flow_hit_1000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.01)
                            sw.WriteLine($"{d.flow_count}\t{d.cm_hit}\t{d.fss_hit}\t{d.cs_hit}");


                using (var sw = new StreamWriter(topo_prefix + "_flow_time_1000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.flow_count}\t{d.cm_time}\t{d.fss_time}\t{d.cs_time}");


                q = from d in data where d.topos == topos && d.flow_count == 200000 orderby d.k ascending select d;

                using (var sw = new StreamWriter(topo_prefix + "_k_appr_200000_095.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.k}\t{d.cm_err}\t{d.fss_err}\t{d.cs_err}");

                using (var sw = new StreamWriter(topo_prefix + "_k_appr_200000_099.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.01)
                            sw.WriteLine($"{d.k}\t{d.cm_err}\t{d.fss_err}\t{d.cs_err}");

                using (var sw = new StreamWriter(topo_prefix + "_k_hit_200000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.01)
                            sw.WriteLine($"{d.k}\t{d.cm_hit}\t{d.fss_hit}\t{d.cs_hit}");

                using (var sw = new StreamWriter(topo_prefix + "_k_time_200000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.k}\t{d.cm_time}\t{d.fss_time}\t{d.cs_time}");

                using (var sw = new StreamWriter(topo_prefix + "_k_hh_1000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.k}\t{d.hh.hh_cm1}\t{d.hh.hh_fss1}\t{d.hh.hh_cs1}");

                using (var sw = new StreamWriter(topo_prefix + "_k_hh_2000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.k}\t{d.hh.hh_cm2}\t{d.hh.hh_fss2}\t{d.hh.hh_cs2}");

                using (var sw = new StreamWriter(topo_prefix + "_k_hh_10000.txt"))
                    foreach (DataLine d in q)
                        if (d.threshold == 0.005)
                            sw.WriteLine($"{d.k}\t{d.hh.hh_cm10}\t{d.hh.hh_fss10}\t{d.hh.hh_cs10}");
            }
        }

        #region tools

        private static double ElephantCover(IEnumerable<(double, double)> list, double threshold) {
            var tuples = list.ToList();
            var count = (int) (threshold * tuples.Count());
            var list1 = tuples.OrderByDescending(t => t.Item1).Take(count);
            var list2 = tuples.OrderByDescending(t => t.Item2).Take(count);
            var join = list1.Intersect(list2).Count();
            return (double) join / count;
        }

        private static List<double> RelativeErrorOfTop(IEnumerable<(double, double)> list, double threshold) {
            var list1 = list;
            list1 = list1.OrderByDescending(t => t.Item1);
            IEnumerable<(double, double)> valueTuples = list1 as (double, double)[] ?? list1.ToArray();
            var q = from tuple in valueTuples.Take((int) (threshold * valueTuples.Count())) select (Math.Abs(tuple.Item2 - tuple.Item1) / tuple.Item1);
            return q.ToList();
        }

        private static List<double> HHFilter(IEnumerable<(double, double)> list, double threshold) {
            var list1 = list;
            var total = list.Sum(t => t.Item1);
            list1 = list1.OrderByDescending(t => t.Item1);
            IEnumerable<(double, double)> valueTuples = list1 as List<Tup> ?? list1.ToList();
            valueTuples = valueTuples.Where(t => t.Item1 > total * threshold);
            var q = from tuple in valueTuples where tuple.Item1 >= total * threshold select (Math.Abs(tuple.Item2 - tuple.Item1) / tuple.Item1);
            return q.ToList();
        }

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

        static List<ReroutedFlow> ReRouteTopWithSketch(Topology topo, List<Flow> flowSet, ITopoSketch<Flow, ElemType> sketch, double thres) =>
            ReRouteTopWithSketch(topo, flowSet, sketch, thres, Greedy.FindPath);

        static List<ReroutedFlow> ReRouteTopWithSketch(Topology topo, List<Flow> flowSet, ITopoSketch<Flow, ElemType> sketch, double thres, RoutingAlgorithm algo) {
            foreach (var sw in topo.Switches) {
                sw.ClearFlow();
            }

            flowSet.Sort((f1, f2) => (int) (f2.Traffic - f1.Traffic));

            var newFlow = new List<ReroutedFlow>();
            foreach (Flow flow in flowSet) {
                //ReroutedFlow newf = new ReroutedFlow(flow) {Traffic = sketch.Query(flow), OriginTraffic = flow.Traffic};
                ReroutedFlow newf = new ReroutedFlow(flow) {Traffic = sketch.Query(flow), OriginTraffic = flow.Traffic};
                newFlow.Add(newf);
            }

            ReRoute(newFlow, algo, (int) (thres * flowSet.Count));
            return newFlow;
        }

        #endregion
    }
}