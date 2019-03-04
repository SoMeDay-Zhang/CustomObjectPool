using System;
using System.Collections.Generic;
using System.Threading;

namespace CustomObjectPool
{
    public sealed class LIFOAccessModel<T> : Stack<T>, IAccessMode<T>
    {
        private readonly int _capacity;
        private readonly Func<T> _func;
        private int _count;

        public LIFOAccessModel(int capacity, Func<T> func) : base(capacity)
        {
            _capacity = capacity;
            _func = func;
            InitialStack();
        }

        public T Rent()
        {
            Interlocked.Increment(ref _count);
            return _capacity < _count ? _func.Invoke() : Pop();
        }

        public void Return(T item)
        {
            if (_count > _capacity)
            {
                var disposable = (IDisposable)item;
                disposable.Dispose();
            }
            else
            {
                Push(item);
            }
            Interlocked.Decrement(ref _count);
        }

        private void InitialStack()
        {
            for (var i = 0; i < _capacity; i++)
            {
                Push(_func.Invoke());
            }
            Console.WriteLine("LIFO Pool Initial Finished");
        }
    }
}