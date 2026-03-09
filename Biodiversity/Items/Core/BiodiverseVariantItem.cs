using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items
{
    /// <summary>
    /// Represents a BiodiverseItem that supports multiple randomly selected variants when spawned, with support for saving/loading the selected variant in the save file
    /// </summary>
    public class BiodiverseVariantItem : BiodiverseItem
    {
        [Tooltip("The mesh renderer that is affected by the random variant selection (if any).")]
        public MeshRenderer meshRenderer;

        [Tooltip("The mesh filter that is affected by the random variant selection (if any).")]
        public MeshFilter meshFilter;

        /// <summary>
        /// The actual variant ID of the item, used to determine which variant is currently active, or -1 if no variant has been selected yet
        /// </summary>
        private int actualVariantID = -1;

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
            return actualVariantID;
        }

        public override void LoadItemSaveData(int saveData)
        {
            ChooseVariant(saveData);
        }

        [ServerRpc]
        private void ChooseVariantServerRpc()
        {
            if (actualVariantID != -1)
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
            if (actualVariantID != -1)
                return;
            actualVariantID = variantID;
            if (variantID == 0)
                return;
            if (itemProperties.meshVariants.Length > 0 && meshFilter != null)
            {
                meshFilter.mesh = itemProperties.meshVariants[variantID];
            }
            else if (itemProperties.materialVariants.Length > 0 && meshRenderer != null)
            {
                meshRenderer.sharedMaterial = itemProperties.materialVariants[variantID];
            }
        }
    }
}
