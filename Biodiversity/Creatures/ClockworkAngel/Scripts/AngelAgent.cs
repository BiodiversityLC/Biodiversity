using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine.AI;

namespace Biodiversity.Creatures.ClockworkAngel.Scripts
{
    internal class AngelAgent : NetworkBehaviour
    {
        public NavMeshAgent Agent;

        private void Start()
        {
            Agent = GetComponent<NavMeshAgent>();
        }
    }
}
