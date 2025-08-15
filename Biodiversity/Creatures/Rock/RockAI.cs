using Biodiversity.Creatures.Aloe;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Biodiversity.Creatures.Rock
{
    internal class RockAI : BiodiverseAI
    {
        float baseScale;
        float scaleFactor = 0.1f;

        float scaleTimer = 0f;
        bool isScalingUp = true;

        public override void Start()
        {
            base.Start();
            baseScale = meshRenderers[0].gameObject.transform.localScale.x;
            SetDestinationToPosition(transform.position);
        }
        public override void Update()
        {
            base.Update();

            scaleTimer += isScalingUp ? Time.deltaTime : -Time.deltaTime;

            if (scaleTimer >= 1f)
            {
                isScalingUp = false;
            }
            else if (scaleTimer <= 0f)
            {
                isScalingUp = true;
            }

            Vector3 baseScaleV3 = new Vector3(baseScale, baseScale, baseScale);
            Vector3 targetScale = baseScaleV3 + baseScaleV3 * scaleFactor;

            meshRenderers[0].gameObject.transform.localScale = Vector3.Lerp(baseScaleV3, targetScale, scaleTimer);
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (IsServer)
            {
                GameObject[] nodes = AloeSharedData.Instance.GetOutsideAINodes();
                SetDestinationToPosition(nodes[UnityEngine.Random.Range(0, nodes.Length)].transform.position);
            }
        }
    }
}
