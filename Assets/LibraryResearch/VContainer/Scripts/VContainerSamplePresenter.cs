using System;
using VContainer.Unity;

namespace LibraryResearch.VContainerSample
{
    public sealed class VContainerSamplePresenter : IStartable, IDisposable
    {
        private readonly VContainerSampleSettings settings;
        private readonly ICounterService counterService;
        private readonly VContainerBasicSample view;

        public VContainerSamplePresenter(
            VContainerSampleSettings settings,
            ICounterService counterService,
            VContainerBasicSample view)
        {
            this.settings = settings;
            this.counterService = counterService;
            this.view = view;
        }

        public void Start()
        {
            counterService.ValueChanged += HandleValueChanged;
            view.IncrementRequested += counterService.Increment;
            view.ResetRequested += counterService.Reset;
            view.Render(settings.Title, counterService.Value);
        }

        public void Dispose()
        {
            counterService.ValueChanged -= HandleValueChanged;
            view.IncrementRequested -= counterService.Increment;
            view.ResetRequested -= counterService.Reset;
        }

        private void HandleValueChanged(int value)
        {
            view.Render(settings.Title, value);
        }
    }
}
