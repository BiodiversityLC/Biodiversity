using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;
using Random = UnityEngine.Random;

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

        private enum MalfunctionID
        {
            WALKIE,
            SHIPDOORS,
            RADARBLINK,
            LIGHTSOUT
        }

        [SerializeField] private AudioClip callSound;

        private float callTimer = 30;
        private float malfunctionTimer = 0;
        private bool setDestCalledAlready = false;
        MalfunctionID malfunction;

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

            malfunctionTimer -= Time.deltaTime;

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


        [ClientRpc]
        public void ToggleAllWalkiesClientRpc()
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.walkieTalkieLight.enabled == true)
                {
                    walkie.SwitchWalkieTalkieOn(false);
                    continue;
                }
                walkie.SwitchWalkieTalkieOn(true);
            }
        }

        [ClientRpc]
        public void ToggleShipDoorsClientRpc()
        {
            HangarShipDoor door = FindObjectOfType<HangarShipDoor>();
            if (door.shipDoorsAnimator.GetBool("Closed"))
            {
                door.shipDoorsAnimator.SetBool("Closed", false);
                return;
            }
            door.shipDoorsAnimator.SetBool("Closed", true);
        }


        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (malfunctionTimer >= 0)
            {
                switch (malfunction)
                {
                    case MalfunctionID.WALKIE:
                        ToggleAllWalkiesClientRpc();
                        break;
                    case MalfunctionID.SHIPDOORS:
                        ToggleShipDoorsClientRpc();
                        break;
                    case MalfunctionID.RADARBLINK:
                        StartOfRound.Instance.mapScreen.SwitchRadarTargetForward(false);
                        break;
                    case MalfunctionID.LIGHTSOUT:
                        FindObjectOfType<ShipLights>().SetShipLightsBoolean(false);
                        break;
                }
            }
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
                    malfunctionTimer = 10;

                    malfunction = (MalfunctionID)Random.Range(0, Enum.GetValues(typeof(MalfunctionID)).Length);
                    BiodiversityPlugin.Logger.LogInfo("Setting malfunction to " + malfunction.ToString());

                    SwitchToBehaviourClientRpc((int)State.PERCH);
                    break;
            }
        }
    }
}
