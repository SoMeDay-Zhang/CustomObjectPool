# .NET Core Object Pooling(对象池)实现
---
在文章开始之前首先要思考的问题是为什么要建立对象池。这和.NET垃圾回收机制有关，正如下面引用所说，内存不是无限的，垃圾回收器最终要回收对象，释放内存。尽管.NET为垃圾回收已经进行了大量优化，例如将托管堆划分为 3 Generations（代）并设定新建的对象回收的最快，新建的短生命周期对象将进入 Gen 0（新建对象大于或等于 85,000 字节将被看作大对象，直接进入 Gen 2），而 Gen 0 通常情况下分配比较小的内存，因此Gen 0 将回收的非常快。而高频率进行垃圾回收导致 CPU 使用率过高，当 Gen 2 包含大量对象时，回收垃圾也将产生性能问题。
>.NET 的垃圾回收器管理应用程序的内存分配和释放。 每当有对象新建时，公共语言运行时都会从托管堆为对象分配内存。 只要托管堆中有地址空间，运行时就会继续为新对象分配空间。 不过，内存并不是无限的。 垃圾回收器最终必须执行垃圾回收来释放一些内存。 垃圾回收器的优化引擎会根据所执行的分配来确定执行回收的最佳时机。 执行回收时，垃圾回收器会在托管堆中检查应用程序不再使用的对象，然后执行必要的操作来回收其内存。[参考](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/)

## 构造对象池
.Net Core 在（Base Class Library）基础类型中添加了 ArrayPool，但 ArrayPool 只适用于数组。针对自定义对象，[参考MSDN](https://docs.microsoft.com/en-us/dotnet/standard/collections/thread-safe/how-to-create-an-object-pool)有一个实现，但没有初始化池大小，且从池里取对象的方式比较粗糙，完整的对象池应该包含：
* 池大小
* 初始化委托
* 实例存取方式（FIFO、LIFO 等自定义方式，根据个人需求实现获取实例方式）
* 获取实例策略


#### 1. 定义对象存取接口，以实现多种存取策略，例如 FIFO、LIFO
```csharp
/// <summary>
/// 对象存取方式
/// </summary>
public interface IAccessMode<T>
{
    /// <summary>
    /// 租用对象
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    T Rent();
    
    /// <summary>
    /// 返回实例
    /// </summary>
    /// <param name="item"></param>
    void Return(T item);
}
```

#### 2. 实现存取策略
##### FIFO
FIFO通过Queue实现，[参考](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1?view=netcore-2.2)
```csharp
public sealed class FIFOAccessMode<T> : Queue<T>, IAccessMode<T>
{
    private readonly int _capacity;
    private readonly Func<T> _func;
    private int _count;

    public FIFOAccessMode(int capacity, Func<T> func) : base(capacity)
    {
        _capacity = capacity;
        _func = func;
        InitialQueue();
    }

    public T Rent()
    {
        Interlocked.Increment(ref _count);
        return _capacity < _count ? _func.Invoke() : Dequeue();
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
            Enqueue(item);
        }
        Interlocked.Decrement(ref _count);
    }

    private void InitialQueue()
    {
        for (var i = 0; i < _capacity; i++)
        {
            Enqueue(_func.Invoke());
        }
    }
}
```
##### LIFO
在LIFO中借助Stack特性实现进栈出栈，因此该策略继承自Stack，[参考](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1?view=netcore-2.2)
```csharp
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
    }
}
```

**注意**：以上两个实现都遵循池容量不变原则，但租用的实例可以超过对象池大小，返还时还将检测该实例直接释放还是进入池中。而如何控制池大小和并发将在下面说明。

#### 3.Pool实现
```csharp
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

```
在Pool中如何控制程序池并发，这里我们引入了 [Semaphore](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphore?view=netcore-2.2) 以控制并发，这里将严格控制程序池大小，避免内存溢出。

#### 4.使用

Student 类用作测试
```csharp
public class Student : IDisposable
{
    public string Name { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
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
```

```csharp
public void TestPool()
{
    Func<Student> func = NewStudent;
    var pool = new Pool<Student>(AccessModel.FIFO, 2, func);
    for (var i = 0; i < 3; i++)
    {
        Student temp = pool.Rent();
        //todo:Some operations
        pool.Return(temp);
    }

    Student temp1 = pool.Rent();

    pool.Return(temp1);

    pool.Dispose();
}

public Student NewStudent()
{
    return new Student();
}
```

**总结**：至此，一个完整的对象池建立完毕。

**安装与使用**：现已发布到NuGet服务器，可在程序包管理控制台中输入安装命令使用。
```
Install-Package CustomObjectPool
```
