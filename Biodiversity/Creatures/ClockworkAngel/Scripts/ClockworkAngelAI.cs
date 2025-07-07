using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = System.Random;

namespace Biodiversity.Creatures.ClockworkAngel.Scripts
{
    internal class ClockworkAngelAI : NetworkBehaviour
    {
        public float DefaultUpdateInterval = 0.2f;
        float UpdateInterval = 0;

        public float ScoutHeight = 75;

        public State CurrentState = State.Scout;

        public AngelAgent AngelAgent;
        public GameObject[] AInodes;


        // Random
        public Random Random;

        // Scouting variables
        GameObject TargetNode = null;


        public enum State
        {
            Scout
        }

        void Start()
        {
            AInodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
            Random = new Random();
        }


        void Update()
        {
            if (IsServer)
            {
                if (UpdateInterval < 0)
                {
                    UpdateInterval = DefaultUpdateInterval;
                    AIInterval();
                }
                UpdateInterval -= Time.deltaTime;

                if (CurrentState == State.Scout)
                {
                    transform.position = AngelAgent.transform.position + new Vector3(0, ScoutHeight, 0);
                }
            }
        }

        void AIInterval()
        {
            switch (CurrentState)
            {
                case State.Scout:

                    if (TargetNode == null)
                    {
                        TargetNode = AInodes[Random.Next(0, AInodes.Length - 1)];
                        AngelAgent.Agent.SetDestination(TargetNode.transform.position);
                    }
                    if (AngelAgent.Agent.remainingDistance < AngelAgent.Agent.stoppingDistance)
                    {
                       TargetNode = null;
                    }

                    break;
            }
        }
    }
}
