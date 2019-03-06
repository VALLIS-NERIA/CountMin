using System.Collections.Generic;
using ElemType = System.Int64;

namespace Simulation {

    public delegate T SketchFactory <out T,in TKey>() where T : ISketch<TKey,ElemType>;
    public delegate T SketchFactory <out T>() where T : ISketch<object,ElemType>;

    public interface IFlowRule {
        bool ContainsFlow(Flow f);
    }

    public interface IReversibleSketch <TKey, TValue> : ITopoSketch<TKey, TValue> {
        IEnumerable<TKey> GetAllKeys();
    }

    public interface ITopoSketch <in TKey, TValue> : ISketch<TKey, TValue> {
        int W { get; }
        string SketchClassName { get; }
        void Update(TKey key, TValue value);

        TValue Query(TKey key);

        TValue this[TKey key] { get; }
    }

    public interface ISketch <in TKey, TValue> {
        void Update(TKey key, TValue value);

        TValue Query(TKey key);

    }

    public interface IFS : ITopoSketch<Flow, ElemType> {

        void Init(Topology topo);
    }
}