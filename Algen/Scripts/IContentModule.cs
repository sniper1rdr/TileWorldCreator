namespace AglenRealms.WorldCore
{
    public interface IContentModule
    {
        string ModuleId { get; }
        string ModuleDisplayName { get; }
        ContentModuleKind ModuleKind { get; }
    }
}
