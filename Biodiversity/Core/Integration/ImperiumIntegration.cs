using BepInEx.Bootstrap;

namespace Biodiversity.Core.Integration;

public static class ImperiumIntegration
{
    public static bool IsLoaded => Chainloader.PluginInfos.ContainsKey("giosuel.Imperium");
}