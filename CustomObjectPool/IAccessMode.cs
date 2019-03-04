namespace CustomObjectPool
{
    /// <summary>
    /// 对象存取方式
    /// </summary>
    public interface IAccessMode<T>
    {
        /// <summary>
        /// 租用对象
        /// </summary>
        /// <returns></returns>
        T Rent();
        
        /// <summary>
        /// 返回实例
        /// </summary>
        /// <param name="item"></param>
        void Return(T item);
    }
}
