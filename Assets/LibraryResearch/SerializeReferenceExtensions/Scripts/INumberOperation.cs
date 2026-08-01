namespace LibraryResearch.SerializeReferenceExtensionsSample
{
    public interface INumberOperation
    {
        string DisplayName { get; }
        float Execute(float input);
    }
}
