using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

#region copyright

/*
 * This is free and unencumbered software released into the public domain.
 * 
 * Anyone is free to copy, modify, publish, use, compile, sell, or
 * distribute this software, either in source code form or as a compiled
 * binary, for any purpose, commercial or non-commercial, and by any
 * means.
 * 
 * In jurisdictions that recognize copyright laws, the author or authors
 * of this software dedicate any and all copyright interest in the
 * software to the public domain. We make this dedication for the benefit
 * of the public at large and to the detriment of our heirs and
 * successors. We intend this dedication to be an overt act of
 * relinquishment in perpetuity of all present and future rights to this
 * software under copyright law.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 * 
 * For more information, please refer to <http://unlicense.org>
 * 
 */

#endregion


// credit to: https://github.com/JetStream96/MinMaxHeap
namespace Simulation {
    // A wrapper of MinHeap<TKey, TValue, TDictionary>.
    // Uses Dictionary<TKey, int> as TDictionary.
    //
    public class MinHeap <TKey, TValue> : IReadOnlyDictionary<TKey, TValue> {
        private MinHeap<TKey, TValue, Dictionary<TKey, int>> heap;

        public MinHeap(IEnumerable<KeyValuePair<TKey, TValue>> items, IComparer<TValue> comparer) { heap = new MinHeap<TKey, TValue, Dictionary<TKey, int>>(items, comparer); }

        public MinHeap(IEnumerable<KeyValuePair<TKey, TValue>> items)
            : this(items, Comparer<TValue>.Default) { }

        public MinHeap(IComparer<TValue> comparer)
            : this(new KeyValuePair<TKey, TValue>[0], comparer) { }

        public MinHeap() : this(Comparer<TValue>.Default) { }

        public KeyValuePair<TKey, TValue> Min => heap.Min;

        public int Count => heap.Count;

        public IEnumerable<TKey> Keys => heap.Keys;

        public IEnumerable<TValue> Values => heap.Values;

        /// <summary>
        /// Gets the value correspoinding to the given key.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public TValue this[TKey key] => heap[key];

        /// <summary>
        /// Returns whether the key exists.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public bool ContainsKey(TKey key) {
            return heap.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value) { return heap.TryGetValue(key, out value); }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() { return heap.GetEnumerator(); }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        /// <summary>
        /// Extract the smallest element.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public KeyValuePair<TKey, TValue> ExtractMin() {
            return heap.ExtractMin();
        }

        /// <summary>
        /// Insert the key and value.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void Add(TKey key, TValue val) {
            heap.Add(key, val);
        }

        /// <summary>
        /// Modify the value corresponding to the given key.
        /// </summary>
        /// <exception cref="ArgumentNullException">Key is null.</exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public void ChangeValue(TKey key, TValue newValue) {
            heap.ChangeValue(key, newValue);
        }
    }

    #region Base Class

    /// <typeparam name="TDictionary">
    /// Maps a key to the index of the corresponding KeyValuePair 
    /// in the list.</typeparam>
    public class MinHeap <TKey, TValue, TDictionary> : IReadOnlyDictionary<TKey, TValue>
        where TDictionary : IDictionary<TKey, int>, new() {
        private List<KeyValuePair<TKey, TValue>> values;
        private TDictionary indexInList;
        private IComparer<TValue> comparer;

        public MinHeap(IEnumerable<KeyValuePair<TKey, TValue>> items,
                       IComparer<TValue> comparer) {
            values = new List<KeyValuePair<TKey, TValue>>();
            indexInList = new TDictionary();
            this.comparer = comparer;
            values.Add(default(KeyValuePair<TKey, TValue>));
            AddItems(items);
        }

        public MinHeap(IEnumerable<KeyValuePair<TKey, TValue>> items)
            : this(items, Comparer<TValue>.Default) { }

        public MinHeap(IComparer<TValue> comparer)
            : this(new KeyValuePair<TKey, TValue>[0], comparer) { }

        public MinHeap() : this(Comparer<TValue>.Default) { }

        public KeyValuePair<TKey, TValue> Min => values[1];

        public int Count => values.Count - 1;

        public IEnumerable<TKey> Keys => indexInList.Keys;

        public IEnumerable<TValue> Values => values.Select(kv => kv.Value);

        /// <summary>
        /// Gets the value correspoinding to the given key.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public TValue this[TKey key] => values[indexInList[key]].Value;

        /// <summary>
        /// Returns whether the key exists.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public bool ContainsKey(TKey key) {
            return indexInList.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            int index;
            if (indexInList.TryGetValue(key, out index)) {
                value = values[index].Value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            values.Skip(1).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Extract the smallest element.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public KeyValuePair<TKey, TValue> ExtractMin() {
            int count = Count;

            if (count == 0) {
                throw new InvalidOperationException("Heap is empty.");
            }

            var min = Min;
            values[1] = values[count];
            values.RemoveAt(count);
            indexInList.Remove(min.Key);

            if (values.Count > 1) {
                indexInList[values[1].Key] = 1;
                BubbleDown(1);
            }

            return min;
        }

        /// <summary>
        /// Insert the key and value.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void Add(TKey key, TValue val) {
            int count = values.Count;
            indexInList.Add(key, count);
            values.Add(new KeyValuePair<TKey, TValue>(key, val));
            BubbleUp(count);
        }

        /// <summary>
        /// Modify the value corresponding to the given key.
        /// </summary>
        /// <exception cref="ArgumentNullException">Key is null.</exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public void ChangeValue(TKey key, TValue newValue) {
            int index = indexInList[key];
            int compareVal = comparer.Compare(newValue, values[index].Value);
            values[index] = new KeyValuePair<TKey, TValue>(
                values[index].Key, newValue);

            if (compareVal > 0) {
                BubbleDown(index);
            }
            else if (compareVal < 0) {
                BubbleUp(index);
            }
        }

        private void BubbleUp(int index) {
            int parent = index / 2;

            while (index > 1 && CompareResult(parent, index) > 0) {
                Exchange(index, parent);
                index = parent;
                parent /= 2;
            }
        }

        private void BubbleDown(int index) {
            int min;

            while (true) {
                int left = index * 2;
                int right = index * 2 + 1;

                if (left < values.Count &&
                    CompareResult(left, index) < 0) {
                    min = left;
                }
                else {
                    min = index;
                }

                if (right < values.Count &&
                    CompareResult(right, min) < 0) {
                    min = right;
                }

                if (min != index) {
                    Exchange(index, min);
                    index = min;
                }
                else {
                    return;
                }
            }
        }

        // JIT compiler does not inline this method without this 
        // attribute. Inlining gives a small performance
        // increase.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CompareResult(int index1, int index2) {
            return comparer.Compare(values[index1].Value, values[index2].Value);
        }

        private void Exchange(int index, int max) {
            var tmp = values[index];
            values[index] = values[max];
            values[max] = tmp;

            indexInList[values[index].Key] = index;
            indexInList[values[max].Key] = max;
        }

        private void AddItems(IEnumerable<KeyValuePair<TKey, TValue>> items) {
            int index = values.Count;

            foreach (var i in items) {
                values.Add(i);
                indexInList.Add(i.Key, index);
                index++;
            }

            for (int i = values.Count / 2; i >= 1; i--) {
                BubbleDown(i);
            }
        }
    }

    #endregion
}