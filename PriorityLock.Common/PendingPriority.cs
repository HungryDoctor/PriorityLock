using System;

namespace PriorityLock.Common
{
    public class PendingPriority : IEquatable<PendingPriority>, IComparable<PendingPriority>
    {
        public readonly int Priority;
        public int PendingThreadsCounter;


        public PendingPriority(int priority)
        {
            Priority = priority;
        }


        public int CompareTo(PendingPriority other)
        {
            if (other == null)
            {
                return -1;
            }

            return Priority.CompareTo(other.Priority);
        }

        public override bool Equals(object obj)
        {
            if (obj is PendingPriority pendingPriority)
            {
                return Equals(pendingPriority);
            }

            return base.Equals(obj);
        }

        public bool Equals(PendingPriority other)
        {
            return other != null && Priority == other.Priority;
        }

        public override int GetHashCode()
        {
            return Priority.GetHashCode();
        }

        public override string ToString()
        {
            return $"Priority: '{Priority}', PendingThreadsCounter: '{PendingThreadsCounter}'";
        }
    }
}
