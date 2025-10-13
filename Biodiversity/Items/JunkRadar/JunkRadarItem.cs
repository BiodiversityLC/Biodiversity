namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarItem : BiodiverseItem
    {
        protected override string GetLogPrefix()
        {
            return $"[JunkRadarItem {BioId}]";
        }
    }
}
