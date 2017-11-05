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
    class Program {

        static List<Flow> ReRouteWithSketch (string topoJson, string flowJson,ISketch<Flow,long> sketch) {
            var topo = LoadTopo(topoJson);
            var flowSet = LoadFlow(flowJson, topo);
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

        static Topology LoadTopo(string fileName) => JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName)).ToTopology();
        static List<Flow> LoadFlow(string fileName, Topology topo) => JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName)).ToCoflow(topo);

        static void _Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            foreach (int k in new[] {10, 25, 50, 100, 200, 400, 700, 1000, 2000}) {
                Topology topo = LoadTopo("hyperx5.json");
                var flowSet = LoadFlow($"28_countmax_ospf_10000_{k}.json", topo);


                //var cm = new CountMax<Flow, Switch>(k, 2);
                //foreach (Flow flow in flowSet) {
                //    flow.Assign();
                //    cm.Update(flow, (ulong) flow.Traffic);
                //}
                //foreach (Flow flow in flowSet) {
                //    flow.Traffic = cm.Query(flow);
                //}
                var json = flowSet.ToCoflowJson(topo);
                string str = JsonConvert.SerializeObject(json);
                using (var sw = new StreamWriter($"28_countmax_ospf_10000_{k}.json")) {
                    sw.Write(str);
                }
            }
        }

        static void Main() {
            Directory.SetCurrentDirectory(@"..\..\..\data");
            Console.WriteLine($"{"k",10}{"max",10},{"avg.",10}{"delta",10}");
            foreach (int k in new[] {0,/*10, 25, 50, 100, 200, 400, 700, 1000, 2000*/}) {
                Topology topo = LoadTopo("hyperx5.json");
                var flowReal = LoadFlow("28_ospf_10w.json", topo);
                var flowSet = LoadFlow($"28_countmax_greedy_10000_{k}.json", topo);
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
                cm.Update(flow, (ulong) flow.Traffic);
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