using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    using Path = List<Switch>;

    public static class OSPF {
        public class Memo {
            public Path Route;
            public Dictionary<Switch, bool> Visited;
            public Path ShortestPath;

            public int Min => ShortestPath?.Count ?? int.MaxValue;

            public Memo() {
                Route = new Path();
                Visited = new Dictionary<Switch, bool>();
                ShortestPath = new Path();
            }
        }

        public static Path FindPath(Switch src, Switch dst, Topology topo, Memo memo) {
            // begin
            if (memo == null) {
                memo = new Memo();
            }
            memo.Route.Add(src);

            // end
            if (src == dst) {
                var path = new Path(memo.Route);
                if (path.Count < memo.Min) {
                    memo.ShortestPath = path;
                }
                return path;
            }

            // inside
            Path shortest = null;
            memo.Visited[src] = true;
            foreach (var sw in src.LinkedSwitches) {
                memo.Route.Add(sw);
                if (memo.Route.Count > memo.ShortestPath.Count || memo.Visited[sw]) {
                    goto pop;
                }
                var path = FindPath(sw, dst, topo, memo);
                var min = shortest?.Count ?? int.MaxValue;
                if (path.Count < min) {
                    shortest = path;
                }
                pop:
                memo.Route.RemoveAt(memo.Route.Count - 1);
            }
            memo.Visited[src] = false;
            return shortest;
        }
    }

    public static class Greedy {
        public class Memo {
            public Path Route;
            public Dictionary<Switch, bool> Visited;
            public Path ShortestPath;

            public double Min => ShortestPath?.GetMaxLoad() ?? double.MaxValue;

            public Memo() {
                Route = new Path();
                Visited = new Dictionary<Switch, bool>();
                ShortestPath = new Path();
            }
        }

        public static double GetMaxLoad(this Path path) {
            double load = 0;
            using (var iter = path.GetEnumerator()) {
                while (true) {
                    var sw = iter.Current;
                    if (!iter.MoveNext()) {
                        break;
                    }
                    var sw2 = iter.Current;
                    load += sw.LinkLoad[sw2];
                }
                return load;
            }
        }

        public static Path FindPath(Switch src, Switch dst, Topology topo, OSPF.Memo memo) {
            // begin
            if (memo == null) {
                memo = new OSPF.Memo();
            }
            memo.Route.Add(src);

            // end
            if (src == dst) {
                var path = new Path(memo.Route);
                if (path.GetMaxLoad() < memo.Min) {
                    memo.ShortestPath = path;
                }
                return path;
            }

            // inside
            Path shortest = null;
            memo.Visited[src] = true;
            foreach (var sw in src.LinkedSwitches) {
                memo.Route.Add(sw);
                if (memo.Route.GetMaxLoad() > memo.ShortestPath.GetMaxLoad() || memo.Visited[sw]) {
                    goto pop;
                }
                var path = FindPath(sw, dst, topo, memo);
                var min = shortest?.GetMaxLoad() ?? double.MaxValue;
                if (path.GetMaxLoad() < min) {
                    shortest = path;
                }
                pop:
                memo.Route.RemoveAt(memo.Route.Count - 1);
            }
            memo.Visited[src] = false;
            return shortest;
        }
    }
}