using System;

namespace LibraryResearch.VContainerSample
{
    public sealed class CounterService : ICounterService
    {
        private readonly VContainerSampleSettings settings;

        public CounterService(VContainerSampleSettings settings)
        {
            this.settings = settings;
            Value = settings.InitialValue;
        }

        public int Value { get; private set; }
        public event Action<int> ValueChanged;

        public void Increment()
        {
            Value += settings.Increment;
            ValueChanged?.Invoke(Value);
        }

        public void Reset()
        {
            Value = settings.InitialValue;
            ValueChanged?.Invoke(Value);
        }
    }
}
