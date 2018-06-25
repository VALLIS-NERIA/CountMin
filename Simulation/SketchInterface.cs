using System.Collections.Generic;
using ElemType = System.Int64;

namespace Simulation {
    public interface IReversibleSketch <TKey, TValue> : ITopoSketch<TKey, TValue> {
        IEnumerable<TKey> GetAllKeys();
    }

    public interface ITopoSketch <in TKey, TValue> {
        int W { get; }

        void Update(TKey key, TValue value);

        TValue Query(TKey key);

        TValue this[TKey key] { get; }
    }

    public interface ISketch <TValue> {
        void Update(object key, TValue value);

        TValue Query(object key);

    }

    public interface IFS : ITopoSketch<Flow, ElemType> {
        string SketchClassName { get; }

        void Init(Topology topo);
    }
}