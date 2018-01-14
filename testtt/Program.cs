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
using anal = System.Console;


namespace testtt {
    internal class Program {
        private static int[] k_list = {100, 200, 400, 700, 1000, 1500, 2000, 2500, 3000};
        private static string[] topo_list = {"fattree8", "hyperx9"};

        private static Topology LoadTopo(string fileName)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName)).ToTopology()
                   : JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName + ".json")).ToTopology();

        private static List<Flow> LoadFlow(string fileName, Topology topo)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName)).ToCoflow(topo)
                   : JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName + ".json")).ToCoflow(topo);

        private static List<double> Filter(List<(double, double)> list, double threshold) {
            var list1 = list;
            list1.Sort((tuple1, tuple2) => -tuple1.Item1.CompareTo(tuple2.Item1));
            var q = from tuple in list1.Take((int) (threshold * list1.Count)) select (Math.Abs(tuple.Item2 - tuple.Item1) / tuple.Item1);
            return q.ToList();
        }
        private static void Main(string[] args) {
            Directory.SetCurrentDirectory(@"..\..\..\data");

            //anal.WriteLine("topo, k, flow_count, threshold, cm_avg, cm_min, cm_max, cm_hit, cm_time, cs_avg, cs_min, cs_max, cs_hit, cs_time");
            foreach (string topos in new[]{"hyperx9"}) {
                foreach (int k in new[]{5000}) {
                    for (int d =1;d<16;d++) {

                        int.TryParse("12", out int x);

                        int y;
                        int.TryParse("12", out y);

                        var topo = LoadTopo(topos + ".json");
                        var fin = $"zipf_100000_{topos}_OSPF.json";
                        var flowSet = LoadFlow(fin, topo);
                        var flow_count = flowSet.Count;
                        var cm = new CountMax.SwitchSketch(k, d);

                        //Console.WriteLine($"{flow_count} in {topos} initing");
                        var t00 = DateTime.Now;
                        var even = 0;
                        var odd = 0;
                        foreach (Flow flow in flowSet) {
                            //var h = flow.GetHashCode();
                            //var m = 0;
                            //for (int i = 0; i < 32; i++) {
                            //    var c = h >> i & 1;
                            //    m += c;
                            //}
                            //m = m % 2;
                            //if (m == 0) {
                            //    odd++;
                            //}
                            //else {
                            //    even++;
                            //}
                            cm.Update(flow, (Int64) flow.Traffic);
                        }
                        var t01 = DateTime.Now;





                        //Console.WriteLine($"{flow_count} in {topos} finished in {t0.TotalMilliseconds}/{t1.TotalMilliseconds}, rerouting...");

                        var list = new List<Tup>();
                        var list2 = new List<Tup>();
                        using (var data = new StreamWriter($"data1/{topos}_{k}.csv")) {
                            //data.WriteLine("original,cm,cs");
                            foreach (Flow flow in flowSet) {
                                var query = cm.Query(flow);
                                list.Add((flow.Traffic, query));
                                //data.WriteLine($"{flow.Traffic},{query},{query2}");
                            }
                        }

                        foreach (var threshold in new[] {0.98}) {
                            var ll = Filter(list, 1 - threshold);
                            var count = ll.Count(item => item != 0);
                            anal.WriteLine($"{d}, {ll.Average()}");
                        }
                    }
                }
            }
            Console.ReadLine();
        }
    }
}