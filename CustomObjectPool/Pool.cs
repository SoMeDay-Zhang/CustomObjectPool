using System;
using System.Threading;

namespace CustomObjectPool
{
    public class Pool<T> : IDisposable where T : IDisposable
    {
        private int _capacity;
        private IAccessMode<T> _accessMode;
        private readonly object _locker = new object();
        private readonly Semaphore _semaphore;

        public Pool(AccessModel accessModel, int capacity, Func<T> func)
        {
            _capacity = capacity;
            _semaphore = new Semaphore(capacity, capacity);
            InitialAccessMode(accessModel, capacity, func);
        }

        private void InitialAccessMode(AccessModel accessModel, int capacity, Func<T> func)
        {
            switch (accessModel)
            {
                case AccessModel.FIFO:
                    _accessMode = new FIFOAccessMode<T>(capacity, func);
                    break;
                case AccessModel.LIFO:
                    _accessMode = new LIFOAccessModel<T>(capacity, func);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public T Rent()
        {
            _semaphore.WaitOne();
            return _accessMode.Rent();
        }

        public void Return(T item)
        {
            _accessMode.Return(item);
            _semaphore.Release();
        }

        public void Dispose()
        {
            if (!typeof(IDisposable).IsAssignableFrom(typeof(T))) return;

            lock (_locker)
            {
                while (_capacity > 0)
                {
                    var disposable = (IDisposable)_accessMode.Rent();
                    _capacity--;
                    disposable.Dispose();
                }

                _semaphore.Dispose();
            }
        }
    }
}