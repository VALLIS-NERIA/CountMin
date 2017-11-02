using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace Simulation {
    public class Path : List<Switch> {
        // maximum link load in this route
        public double MaxLoad {
            get => GetMaxLoad() + base.Count * 0.1;
        }
        private bool dirty = false;

        public new void Add(Switch sw) {
            //if (Count > 0) {
            //    var ori = base[base.Count-1];
            //    var load = ori.LinkLoad[sw];
            //    if (load > MaxLoad) {
            //        MaxLoad = load;
            //    }
            //}
            base.Add(sw);
        }

        public void Pop() {
            this.RemoveAt(this.Count - 1);
            //this.MaxLoad = GetMaxLoad();
        }

        public double GetMaxLoad() {
            double load = 0;
            using (var iter = base.GetEnumerator()) {
                iter.MoveNext();
                while (true) {
                    var sw = iter.Current;
                    if (!iter.MoveNext()) {
                        break;
                    }
                    var sw2 = iter.Current;
                    var cload = sw.LinkLoad[sw2];
                    if (cload > load) {
                        load = cload;
                    }
                }
                return load;
            }
        }

        public double GetTotalLoad() {
            double load = 0;
            using (var iter = base.GetEnumerator()) {
                iter.MoveNext();
                while (true) {
                    var sw = iter.Current;
                    if (!iter.MoveNext()) {
                        break;
                    }
                    var sw2 = iter.Current;
                    var cload = sw.LinkLoad[sw2];
                    load += cload;
                }
                return load;
            }
        }

        public Path(List<Switch> sw) : base(sw) { }
        public Path(Path sw) : base((List<Switch>) sw) { }

        public Path() : base() { }
    }

    public delegate Path RoutingAlgorithm(Switch src, Switch dst);

    public static class OSPF {
        public class Memo {
            public const int MaxLength=10;
            public Path Route;
            public Dictionary<Switch, bool> Visited;
            public Path ShortestPath;

            public int Min => ShortestPath?.Count ?? MaxLength;

            public Memo() {
                Route = new Path();
                Visited = new Dictionary<Switch, bool>();
                ShortestPath = null;
            }
        }

        public static Path FindPath(Switch src, Switch dst) { return FindPath(src, dst, null); }

        public static Path FindPath(Switch src, Switch dst, Memo memo) {
            // begin
            if (memo == null) {
                memo = new Memo();
                memo.Route.Add(src);
            }

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
                if (memo.Route.Count > memo.Min || (memo.Visited.ContainsKey(sw) && memo.Visited[sw])) {
                    goto pop;
                }
                var path = FindPath(sw, dst, memo);
                if (path == null) {
                    goto pop;
                }
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
            public double Min => ShortestPath?.MaxLoad ?? double.MaxValue;

            public Memo() {
                Route = new Path();
                Visited = new Dictionary<Switch, bool>();
                ShortestPath = null;
            }
        }


        public static Path FindPath(Switch src, Switch dst) {
            var path = OSPF.FindPath(src, dst, null);
            if (path.MaxLoad== 0) {
                return path;
            }
            else {
                return FindPath(src, dst, null, path.Count);
            }
        }

        public static Path FindPath(Switch src, Switch dst, Memo memo, int OspfLength) {
            // begin
            if (memo == null) {
                memo = new Memo();
                memo.Route.Add(src);
            }

            // end
            if (src == dst) {
                var path = new Path(memo.Route);
                if (path.MaxLoad < memo.Min) {
                    memo.ShortestPath = path;
                }
                return path;
            }

            // inside
            Path shortest = null;
            memo.Visited[src] = true;
            foreach (var sw in src.LinkedSwitches) {
                memo.Route.Add(sw);
                if (memo.Route.Count > OspfLength + 4) {
                    goto pop;
                }
                if ( memo.Route.MaxLoad >= memo.Min || (memo.Visited.ContainsKey(sw) && memo.Visited[sw])) {
                    goto pop;
                }
                var path = FindPath(sw, dst, memo, OspfLength);
                if (path == null) {
                    goto pop;
                }
                var min = shortest?.MaxLoad ?? double.MaxValue;
                if (path.MaxLoad < min) {
                    shortest = path;
                }
                pop:
                memo.Route.Pop();
            }
            memo.Visited[src] = false;
            return shortest;
        }
    }
}