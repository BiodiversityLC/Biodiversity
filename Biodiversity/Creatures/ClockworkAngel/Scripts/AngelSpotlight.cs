using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.ClockworkAngel.Scripts
{
    internal class AngelSpotlight : NetworkBehaviour
    {
        public ClockworkAngelAI ParentAngel;

        NavMeshAgent Agent;


        GameObject TargetNode = null;

        private void Start()
        {
            Agent = GetComponent<NavMeshAgent>();
        }

        public void TickSpotlight()
        {
            switch (ParentAngel.CurrentState)
            {
                case ClockworkAngelAI.State.Scout:
                    if (TargetNode == null)
                    {
                        TargetNode = ParentAngel.AInodes[ParentAngel.Random.Next(0, ParentAngel.AInodes.Length - 1)];
                        Agent.SetDestination(TargetNode.transform.position);
                    }
                    if (Agent.remainingDistance < Agent.stoppingDistance)
                    {
                        TargetNode = null;
                    }
                    break;
            }
        }
    }
}
