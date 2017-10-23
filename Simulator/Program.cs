using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Simulation;
using Tup = System.ValueTuple<double, double>;

//using Tuple1=(System.Double, System.Double);

namespace Simulator {
    class Program {
        static void Main(string[] args) {
            Topology topo = JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText("fattree8.json")).ToTopology();
            List<Flow> flowSet = JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText("10000_3_28.json")).ToCoflow(topo);
            var cm = new CountMin<double>(200, 2);
            foreach (Flow flow in flowSet) {
                cm.Update(flow, flow.Traffic);
            }
            var list = new List<Tup>();
            using (var sw = new StreamWriter($"data_{flowSet.Count}.csv")) {
                foreach (Flow flow in flowSet) {
                    list.Add((flow.Traffic, cm.Query(flow)));
                    sw.WriteLine($"{flow.Traffic} , {cm.Query(flow)}");
                }
            }
            using (var sw = new StreamWriter($"analysis_{flowSet.Count}.csv")) {
                //var threshold = 0.9;
                //for (var threshold = 0.9; threshold > 0; threshold -= 0.1) {
                foreach(var t in new[] { 15,0}) {
                    var threshold = t;
                    var ll = Filter(list, threshold);
                    sw.WriteLine($"{t} , {ll.Average()} , {ll.Max()}");
                }
            }
        }

        private static List<double> Filter(List<(double, double)> list, double threshold) {
            //list.Sort((tuple1, tuple2) => -tuple1.Item1.CompareTo(tuple2.Item1));
            //var q = from tuple in list where list.IndexOf(tuple) <= threshold * list.Count select tuple.Item2 / tuple.Item1;
            var q = from tuple in list where tuple.Item1>threshold select tuple.Item2 / tuple.Item1;

            //var s1 = (from tuple in q select tuple.Item1).Sum();
            //var s2 = (from tuple in q select tuple.Item2).Sum();
            return q.ToList();
        }
    }
}