using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items
{
    /// <summary>
    /// Represents a BiodiverseItem that supports multiple randomly selected variants when spawned, with support for saving/loading the selected variant in the save file
    /// </summary>
    public class BiodiverseVariantItem : BiodiverseItem
    {
        [Tooltip("The mesh renderer that is affected by the random material variant selection (if any).")]
        public MeshRenderer meshRenderer;

        [Tooltip("The mesh filter that is affected by the random mesh variant selection (if any).")]
        public MeshFilter meshFilter;

        /// <summary>
        /// Determines if the random variant selection is done only on the material/mesh individually, or if it will instead randomize the material and the mesh at the same type to match a material/mesh linked variant
        /// </summary>
        public bool variantTypesAreLinked = false;

        /// <summary>
        /// The actual variant ID of the item, used to determine which variant is currently active, or -1 if no variant has been selected yet
        /// </summary>
        public int ActualVariantID { private set; get; } = -1;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsHost || IsServer)
            {
                ChooseVariantServerRpc();
            }
        }

        public override int GetItemDataToSave()
        {
            return ActualVariantID;
        }

        public override void LoadItemSaveData(int saveData)
        {
            ChooseVariant(saveData);
        }

        [ServerRpc]
        private void ChooseVariantServerRpc()
        {
            if (ActualVariantID != -1)
                return;
            int maxVariantLength = itemProperties.meshVariants.Length > 0 ? itemProperties.meshVariants.Length : itemProperties.materialVariants.Length > 0 ? itemProperties.materialVariants.Length : 0;
            if (maxVariantLength == 0)
                return;
            ChooseVariantClientRpc(Random.Range(0, maxVariantLength));
        }

        [ClientRpc]
        private void ChooseVariantClientRpc(int variantID)
        {
            ChooseVariant(variantID);
        }

        private void ChooseVariant(int variantID)
        {
            if (ActualVariantID != -1)
                return;
            ActualVariantID = variantID;
            if (variantID == 0)
                return;
            bool hasRandomizedMesh = false;
            if (itemProperties.meshVariants.Length > 0 && meshFilter != null)
            {
                meshFilter.mesh = itemProperties.meshVariants[variantID];
                hasRandomizedMesh = true;
            }
            if (itemProperties.materialVariants.Length > 0 && meshRenderer != null && (!hasRandomizedMesh || variantTypesAreLinked))
            {
                meshRenderer.sharedMaterial = itemProperties.materialVariants[variantID];
            }
        }
    }
}
