using System;

namespace LibraryResearch.VContainerSample
{
    public interface ICounterService
    {
        int Value { get; }
        event Action<int> ValueChanged;
        void Increment();
        void Reset();
    }
}
