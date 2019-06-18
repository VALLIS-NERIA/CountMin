using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using Simulation;
using Simulation.Deployments;
using Simulation.Sketches;
using static Simulation.Utils;
using Switch = Simulation.Switch;

//using Tuple1=(System.Double, System.Double);

namespace Simulator {
    using Tup = System.ValueTuple<double, double>;
    using ElemType = Int64;

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
        internal static int[] count_list1 = {50001, 100001, 150001, 200001, 250001, 300001};

        static  void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
#if DEBUG
            Debug.WriteLine("--DEBUG--");
#endif
            //HalfHalfTest();
            //Filter();
            //TTT();
            IEnumerable<Task> taskList = new List<Task>
            {
                //MscTest()
            };
            var taskArray = taskList.ToArray();
            //RunTask(taskArray, 3, false);

            MscTest();
            Console.WriteLine("Finished.");
            Console.ReadLine();
        }

        public static int RoundDown(this double me, int divisor) {
            int integer = (int) (me / divisor);
            double remain = me / divisor - integer;
            int add = rnd.NextDouble() < remain ? 1 : 0;
            return integer + add;
        }

        private static void MscTest() {
            int k = 1000;
            foreach (int c in count_list1) 
            //foreach (int k in k_list) 
            {
                var sp = Generator.Program.FatTreeGen();
                var flowSet = ((IEnumerable<Flow>)LoadFlow($"Fattree_{c}", sp)).Reverse().Skip(100).ToList();
                //flowSet.ForEach(f => f.Traffic =f.Traffic.RoundDown(1000));
                var msc = new Msc(sp, flowSet, (int) (flowSet[0].Traffic * 10));
                var rules = msc.GetRules();

                var ske = new EgressSketch<CountMax>(() => new CountMax(k, 2), sp, k);
                var skf = new HalfSketch<CountMax>(() => new CountMax(k, 2), sp, k);
                var skr = new RuledSketch<CountMax>(() => new CountMax(k, 2), sp, k, rules);

                foreach (Flow flow in flowSet) {
                    ske.Update(flow, (long) flow.Traffic);
                    skf.Update(flow, (long) flow.Traffic);
                    skr.Update(flow, (long) flow.Traffic);
                }

                var erre = RelativeError2(flowSet, 0.005, ske);
                var errf = RelativeError2(flowSet, 0.005, skf);
                var errr = RelativeError2(flowSet, 0.005, skr);
            Console.WriteLine($"{c}\t {msc.AlgoTime}\t {erre.Average(i=>i.Error)} \t {errf.Average(i => i.Error)} \t {errr.Average(i => i.Error)}\t{ske.MaxLoad}\t{skf.MaxLoad} \t{skr.MaxLoad}");
            }

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

        #region tools

        //private static List<double> SketchError(ISketch<long> sk, IEnumerable<Flow> flowSet) {

        //}

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
            IEnumerable<(double, double)> valueTuples = list1.ToArray();
            var q = from tuple in valueTuples.Take((int) (threshold * valueTuples.Count())) select (Math.Abs(tuple.Item2 - tuple.Item1) / tuple.Item1);
            return q.ToList();
        }

        private struct FlowEsti {
            public double Actual;
            public double Estimated;
            public double Error;

            public FlowEsti(double act, double esti) {
                this.Actual = act;
                this.Estimated = esti;
                this.Error = Math.Abs((esti - act) / act);
            }

            public override string ToString() => $"({this.Actual}, {this.Estimated}, {this.Error})";
        }

        static List<FlowEsti> RelativeError2(List<Flow> flowSet, double threshold, params ISketch<Flow, ElemType>[] sketch) {
            flowSet.Sort((f1, f2) => (int) (f2.Traffic - f1.Traffic));
            var top = flowSet.Take((int) (threshold * flowSet.Count));
            var q = new List<FlowEsti>();
            foreach (Flow f in top) {
                ElemType esti = 0;
                foreach (var s in sketch) {
                    var res = s.Query(f);
                    if (res > esti) esti = res;
                }

                q.Add(new FlowEsti(f.Traffic, esti));
            }

            return q;
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