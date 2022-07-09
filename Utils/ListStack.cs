using System.Collections.Generic;

namespace RabiRiichi.Utils {
    public class ListStack<T> : List<T> {
        public ListStack() { }
        public ListStack(IEnumerable<T> collection) : base(collection) { }
        public ListStack(int capacity) : base(capacity) { }
        public bool Empty => Count == 0;
        public void Push(T item) {
            Add(item);
        }

        public T Pop() {
            var item = Peek();
            RemoveAt(Count - 1);
            return item;
        }

        public T Peek() => this[Count - 1];

        public IEnumerable<T> PopMany(int count) {
            for (var i = 0; i < count; i++) {
                yield return Pop();
            }
        }
    }
}