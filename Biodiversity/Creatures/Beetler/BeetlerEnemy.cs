using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.Beetler
{
    internal class BeetlerEnemy : MonoBehaviour
    {
        ButlerEnemyAI enemyAI;
        GameObject neck;

        public void Start()
        {
            enemyAI = GetComponent<ButlerEnemyAI>();
            neck = transform.Find("MeshContainer").Find("metarig").Find("spine").Find("spine.001").Find("NeckContainer").gameObject;

            // Go my children
            enemyAI.butlerBeesEnemyType = BeetlerHandler.Instance.Assets.ButtletEnemy;

            // I found a bug
            Instantiate(BeetlerHandler.Instance.Assets.BeetlerParts, neck.transform, false);
        }
    }
}
