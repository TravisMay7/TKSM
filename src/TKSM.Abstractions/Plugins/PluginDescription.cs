using System.Collections.Generic;

namespace TKSM.Abstractions.Plugins
{
    /// <summary>Declared contributions of a plugin (handlers, projections, capabilities).</summary>
    public sealed class PluginDescription
    {
        public IReadOnlyCollection<string> JobKinds { get; init; } = new List<string>();
        public IReadOnlyCollection<string> Projections { get; init; } = new List<string>();
        public IReadOnlyCollection<string> Capabilities { get; init; } = new List<string>();
    }
}
