using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

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

        private enum SoundID
        {
            CALL
        }

        [SerializeField] private AudioClip callSound;

        private float callTimer = 30;
        private bool setDestCalledAlready = false;

        public override void Start()
        {
            base.Start();

            agent.Warp(findFarthestNode().position);
        }

        private Transform findFarthestNode()
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

        [ClientRpc]
        public void PlayVoiceClientRpc(int id)
        {
            AudioClip audio = id switch
            {
                0 => callSound,
                _ => null
            };

            creatureVoice.PlayOneShot(audio);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            switch (currentBehaviourStateIndex)
            {
                case (int)State.GOTOSHIP:
                    if (!setDestCalledAlready)
                    {
                        setDestCalledAlready = true;
                        agent.SetDestination(StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 4, 0));
                    }
                    if (Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 4, 0), transform.position) < 5)
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
                    PlayVoiceClientRpc((int)SoundID.CALL);
                    BiodiversityPlugin.Logger.LogInfo("Caw I'm a bird!");
                    callTimer = 60;
                    SwitchToBehaviourClientRpc((int)State.PERCH);
                    break;
            }
        }
    }
}
