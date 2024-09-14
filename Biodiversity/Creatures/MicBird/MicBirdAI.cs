using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAI : BiodiverseAI
    {
        private enum State
        {
            GOTOSHIP,
            PERCH,
            CALL
        }

        private float callTimer = 30;


        public override void Start()
        {
            base.Start();

            agent.Warp(FindFarthestNode().position);
        }

        private Transform FindFarthestNode()
        {
            Transform farnode = null;
            float lowestDistance = 0;
            foreach (GameObject node in RoundManager.Instance.outsideAINodes)
            {
                float nodeDistance = Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position, node.transform.position);
                if (nodeDistance > lowestDistance)
                {
                    lowestDistance = nodeDistance;
                    farnode = node.transform;
                }
            }
            return farnode;
        }

        public override void Update()
        {
            base.Update();
            if (currentBehaviourStateIndex == (int)State.PERCH)
            {
                callTimer -= Time.deltaTime;
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            switch (currentBehaviourStateIndex)
            {
                case (int)State.GOTOSHIP:
                    agent.SetDestination(StartOfRound.Instance.middleOfShipNode.position);
                    if (Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 5, 0), transform.position) < 2)
                    {
                        BiodiversityPlugin.Logger.LogInfo("Perching");
                        SwitchToBehaviourClientRpc((int)State.PERCH);
                    }
                    break;
                case (int)State.PERCH:
                    if (callTimer <= 0)
                    {
                        BiodiversityPlugin.Logger.LogInfo("Calling");
                        SwitchToBehaviourClientRpc((int)State.CALL);
                    }
                    break;
                case (int)State.CALL:
                    BiodiversityPlugin.Logger.LogInfo("Caw I'm a bird!");
                    callTimer = 60;
                    SwitchToBehaviourClientRpc((int)State.PERCH);
                    break;
            }
        }
    }
}
