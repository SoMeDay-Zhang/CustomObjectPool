using System;
using System.Collections.Concurrent;
using CustomObjectPool;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTes
{
    [TestClass]
    public class PoolTest
    {

        [TestMethod]
        public void TestQueue()
        {
            Func<Student> func = NewStudent;
            var students = new FIFOAccessMode<Student>(1, func);
            Student temp1 = students.Rent();
            Assert.IsNotNull(temp1);
            Student temp2 = students.Rent();
            Assert.IsNotNull(temp2);
        }

        [TestMethod]
        public void TestStack()
        {
            Func<Student> func = NewStudent;
            var students = new LIFOAccessModel<Student>(1, func);
            Student temp1 = students.Rent();
            Assert.IsNotNull(temp1);
            Student temp2 = students.Rent();
            Assert.IsNotNull(temp2);
        }

        [TestMethod]
        public void TestPool()
        {
            Func<Student> func = NewStudent;
            var pool = new Pool<Student>(AccessModel.FIFO, 2, func);
            Student temp1 = pool.Rent();
            Assert.IsNotNull(temp1);
            Student temp2 = pool.Rent();
            Assert.IsNotNull(temp2);
            pool.Return(temp2);

            Student temp3 = pool.Rent();
            Assert.IsNotNull(temp3);

            pool.Return(temp1);
            pool.Dispose();
        }

        [TestMethod]
        public void TestConcurrentDic()
        {
            var handlerDic = new ConcurrentDictionary<string, int>();
            handlerDic.TryAdd("abc", 123);
            bool das = handlerDic.TryGetValue("aw", out int temp);

        }

        public Student NewStudent()
        {
            return new Student();
        }
    }

    public sealed class Student : IDisposable
    {
        public string Name { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Student()
        {
            Dispose(false);
        }

        private bool _disposed;

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Name = null;
                //Free any other managed objects here.
            }

            _disposed = true;
        }
    }
}
