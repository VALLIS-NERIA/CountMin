using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace Simulation {
    public class Path : List<Switch> {
        // maximum link load in this route
        public double MaxLoad {
            get => GetMaxLoad();
        }

        public int Length => (this.Count == 0 ? 99999 : this.Count - 1);

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

        public static Path operator +(Path lhs, Path rhs) {
            if (lhs.Last() != rhs.First()) {
                throw new ArgumentException();
            }

            return new Path(lhs.Take(lhs.Count - 1).Concat(rhs).ToList());
        }

        public Path(List<Switch> sw) : base(sw) { }
        public Path(Path sw) : base((List<Switch>) sw) { }

        public Path(Stack<Switch> stack) : base(stack.Reverse().ToList()) { }
        public Path() : base() { }
    }

    public delegate Path RoutingAlgorithm(Switch src, Switch dst);

    public class Floyd {
        private Dictionary<Switch, Dictionary<Switch, Path>> table;
        private Topology topo;

        public Floyd(Topology topo) {
            this.topo = topo;
            this.table = new Dictionary<Switch, Dictionary<Switch, Path>>();
            Init();
        }

        private void Init() {
            foreach (Switch sw1 in topo.Switches) {
                this.table[sw1] = new Dictionary<Switch, Path>();
                this.table[sw1][sw1] = new Path {sw1};
                foreach (Switch sw3 in this.topo.Switches) {
                    this.table[sw1][sw3] = new Path();
                }
                foreach (Switch sw2 in sw1.LinkedSwitches) {
                    this.table[sw1][sw2] = new Path {sw1, sw2};
                }
            }
        }

        public Dictionary<Switch, Dictionary<Switch, Path>> Calc() {
            foreach (Switch k in topo.Switches) {
                foreach (Switch i in topo.Switches) {
                    foreach (Switch j in topo.Switches) {
                        var ij = this.table[i][j];
                        var ik = this.table[i][k];
                        var kj = this.table[k][j];
                        if (ij.Length > ik.Length + kj.Length) {
                            this.table[i][j] = this.table[i][k] + this.table[k][j];
                        }
                    }
                }
            }
            return this.table;
        }
    }

    public static class OSPF {
        public class Memo {
            public const int MaxLength = 10;
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

        private static Dictionary<(Switch src, Switch dst), Path> known = new Dictionary<(Switch src, Switch dst), Path>();

        public static Path FindPath(Switch src, Switch dst) { return FindPath(src, dst, null); }

        public static Path FindPath(Switch src, Switch dst, Memo memo, int maxLength = 15) {
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
                if (memo.Route.Count > maxLength) {
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
        public static double GetMaxLoad(this IEnumerable<Switch> path) {
            double load = 0;
            using (var iter = path.GetEnumerator()) {
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

        public class Memo {
            public Stack<Switch> Route;

            //public Dictionary<Switch, bool> Visited;
            public Path ShortestPath;

            public double MaxLoad = 0;
            public double ShortestPathLoad = double.MaxValue;

            public Memo() {
                Route = new Stack<Switch>();
                //Visited = new Dictionary<Switch, bool>();
                ShortestPath = null;
            }
        }

        public static Path FindPath(Switch src, Switch dst) {
            var path = src.Topology.Floyd[src][dst];
            if (path.MaxLoad == 0) {
                return path;
            }
            else {
                var memo = new Memo();
                memo.Route.Push(src);
                return FindPath(src, dst, memo, path.Count).path;
            }
        }

        public static Path FindPath(Flow pre) {
            var p = new Path(pre.Nodes);
            if (p.MaxLoad == 0) {
                return p;
            }
            else {
                var memo = new Memo();
                memo.Route.Push(pre.First());
                return FindPath(pre.IngressSwitch, pre.OutgressSwitch, memo, p.Count).path;
            }
        }

        public static Path FindPathNoRecursive(Switch src, Switch dst, int length) {
            Dictionary<Switch, bool> visited = new Dictionary<Switch, bool>();
            foreach (Switch sw in src.Topology.Switches) {
                visited.Add(sw, false);
            }
            Stack<Switch> stack = new Stack<Switch>();
            stack.Push(src);
            visited[src] = true;
            double load = 0;
            double shortestLoad = double.MaxValue;
            Path shortestPath = null;
            while (stack.Count != 0) {
                throw new NotImplementedException();
            }
            return shortestPath;
        }

        // memo.Route should be intialized with src inside
        public static (Path path, double load) FindPath(Switch src, Switch dst, Memo memo, int OspfLength) {
            // begin
            //if (memo == null) {
            //    memo = new Memo();
            //    memo.Route.Add(src);
            //}

            // end
            if (src == dst) {
                var path = new Path(memo.Route);
                if (memo.MaxLoad < memo.ShortestPathLoad
                    || (memo.MaxLoad == memo.ShortestPathLoad && memo.Route.Count < memo.ShortestPath.Count)) {
                    memo.ShortestPath = path;
                    memo.ShortestPathLoad = memo.MaxLoad;
                }
                return (path, memo.MaxLoad);
            }

            // inside
            Path shortest = null;
            var shortestLoad = double.MaxValue;
            //memo.Visited[src] = true;
            src.Visited = true;
            foreach (var sw in src.LinkedSwitches) {
                if (sw.Visited) {
                    goto next;
                }
                if (memo.Route.Count + 1 > OspfLength + 4) {
                    goto next;
                }
                // peek
                var oldLoad = memo.MaxLoad;
                var currentLoad = memo.Route.Peek().LinkLoad[sw];
                var newLoad = currentLoad > oldLoad ? currentLoad : oldLoad;

                // chop
                if (newLoad > memo.ShortestPathLoad) {
                    goto next;
                }
                if (newLoad == memo.ShortestPathLoad && memo.Route.Count > memo.ShortestPath.Count) {
                    goto next;
                }

                //push

                memo.MaxLoad = newLoad;
                memo.Route.Push(sw);

                // recursive
                var result = FindPath(sw, dst, memo, OspfLength);


                // update
                if (result.path == null) {
                    goto pop;
                }
                if (result.load > shortestLoad) {
                    goto next;
                }
                else if (result.load < shortestLoad || result.path.Count < shortest.Count) {
                    shortest = result.path;
                    shortestLoad = result.load;
                }

                // pop
                pop:
                memo.MaxLoad = oldLoad;
                memo.Route.Pop();
                next:
                continue;
            }
            src.Visited = false;
            //memo.Visited[src] = false;
            return (shortest, shortestLoad);
        }

        //public static Path FindPathOld(Switch src, Switch dst) {
        //    var path = OSPF.FindPath(src, dst, null);
        //    if (path.MaxLoad == 0) {
        //        return path;
        //    }
        //    else {
        //        return FindPathOld(src, dst, null, path.Count);
        //    }
        //}

        //public static Path FindPathOld(Switch src, Switch dst, Memo memo, int OspfLength) {
        //    // begin
        //    if (memo == null) {
        //        memo = new Memo();
        //        memo.Route.Push(src);
        //    }

        //    // end
        //    if (src == dst) {
        //        var path = new Path(memo.Route);
        //        if (path.MaxLoad < memo.ShortestPathLoad
        //            || (path.MaxLoad == memo.ShortestPathLoad && path.Count < memo.ShortestPath.Count)) {
        //            memo.ShortestPath = path;
        //        }
        //        return path;
        //    }

        //    // inside
        //    Path shortest = null;
        //    memo.Visited[src] = true;
        //    foreach (var sw in src.LinkedSwitches) {
        //        memo.Route.Push(sw);
        //        if (memo.Route.Count > OspfLength + 4) {
        //            goto pop;
        //        }
        //        if (memo.Route.GetMaxLoad() >= memo.ShortestPathLoad
        //            || (memo.Visited.ContainsKey(sw) && memo.Visited[sw])) {
        //            goto pop;
        //        }
        //        if (memo.Route.GetMaxLoad() == memo.ShortestPathLoad && memo.Route.Count > memo.ShortestPath.Count) {
        //            goto pop;
        //        }
        //        var path = FindPathOld(sw, dst, memo, OspfLength);
        //        if (path == null) {
        //            goto pop;
        //        }
        //        var min = shortest?.MaxLoad ?? double.MaxValue;
        //        if (path.MaxLoad < min) {
        //            shortest = path;
        //        }
        //        pop:
        //        memo.Route.Pop();
        //    }
        //    memo.Visited[src] = false;
        //    return shortest;
        //}
    }
}