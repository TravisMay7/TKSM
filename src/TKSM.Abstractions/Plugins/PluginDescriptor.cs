namespace TKSM.Abstractions.Plugins
{
    /// <summary>Discovered plugin descriptor, prior to activation.</summary>
    public sealed class PluginDescriptor
    {
        public string Name { get; init; } = string.Empty;
        public string? DllPath { get; init; }
        public IPluginMetadata? Metadata { get; init; }
    }
}
