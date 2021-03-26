namespace PriorityLock.Common.Interfaces
{
    public interface ILockManager
    {
        ILocker Lock(int priority);
    }
}
