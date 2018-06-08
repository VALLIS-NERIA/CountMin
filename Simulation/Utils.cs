using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Simulation {
    public static class Utils {
        public static void RunTask(Task[] taskArray, int count=3) {
            var begin = DateTime.Now;
            int i = 0;
            int countOld = 0;
            int countReOld = 0;
            int countSkipOld = 0;
            double wait = 0.2;
            while (true) {
                var trd = taskArray.Count(t => t.Status == TaskStatus.Running);
                var finish = taskArray.Count(t => t.Status == TaskStatus.RanToCompletion || t.Status == TaskStatus.Faulted);
                //Console.Write($"\rActive Thread: {trd}, Finished: {finish}, Waiting:{taskArray.Length - i}, Speed: {(int) ((Counter - countOld) / wait)}/s ({(int) ((CounterRerouted - countReOld) / wait)}/{(int) ((CounterSkipped - countSkipOld) / wait)}).                 \r");
                Console.Write(
                    $"\rTime Elapsed: {(DateTime.Now - begin).ToString(@"hh\:mm\:ss")}," +
                    $" Thread: {trd}/{finish}/{taskArray.Length - i}," +
                    $" Speed: {(int) ((Counter - countOld) / wait)}/s ({(int) ((CounterRerouted - countReOld) / wait)}" +
                    $"/{(int) ((CounterSkipped - countSkipOld) / wait)}).                 \r");
                countOld = Counter;
                countReOld = CounterRerouted;
                countSkipOld = CounterSkipped;
                if (trd < count && i < taskArray.Length) {
                    Console.Write("\r");
                    taskArray[i++].Start();
                }
                if (i == taskArray.Length && finish == taskArray.Length) break;
                Thread.Sleep((int) (wait * 1000));
            }
            Task.WaitAll(taskArray);
            Console.WriteLine("\nPress Q to exit.");
            while (true) {
                var c = Console.ReadKey();
                if (c.Key == ConsoleKey.Q) {
                    Environment.Exit(0);
                }
            }
        }

        public static Flow ReRoute(Flow flow, RoutingAlgorithm algo) {
            var src = flow.IngressSwitch;
            var dst = flow.OutgressSwitch;
            return new Flow(algo(src, dst)) {Traffic = flow.Traffic};
        }

        public static int Counter = 0;
        public static int CounterRerouted = 0;
        public static int CounterSkipped = 0;

        public static void ReRoute(IEnumerable<Flow> flowSet, RoutingAlgorithm algo, int count = 0) {
            if (count == 0) {
                int i = 1;
                //foreach (Flow flow in flowSet) {
                foreach (Flow flow in flowSet) {
                    // DO NOT REROUTE BLANK FLOWS
                    ++Counter;
                    if (flow.Traffic <= 1) {
                        CounterSkipped++;
                        continue;
                    }
                    var src = flow.IngressSwitch;
                    var dst = flow.OutgressSwitch;
                    flow.OverrideAssign(algo(src, dst));
                    CounterRerouted++;
                }
            }
            else {
                int i = 1;
                foreach (Flow flow in flowSet) {
                    // DO NOT REROUTE BLANK FLOWS
                    ++Counter;
                    if (flow.Traffic == 0) {
                        continue;
                    }
                    var src = flow.IngressSwitch;
                    var dst = flow.OutgressSwitch;
                    flow.OverrideAssign(algo(src, dst));
                    if (++i > count) {
                        break;
                    }
                    //Console.Write($"\r{i++}");
                }
            }
        }

        public static Topology LoadTopo(string fileName)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName)).ToTopology()
                   : JsonConvert.DeserializeObject<TopologyJson>(File.ReadAllText(fileName + ".json")).ToTopology();

        public static List<Flow> LoadFlow(string fileName, Topology topo)
            => fileName.EndsWith(".json")
                   ? JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName)).ToCoflow(topo)
                   : JsonConvert.DeserializeObject<CoflowJson>(File.ReadAllText(fileName + ".json")).ToCoflow(topo);
    }
}
