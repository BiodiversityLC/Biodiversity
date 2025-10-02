using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.Rock
{
    public class RockAnimHandler : MonoBehaviour
    {
        public RockAI RockAI;

        public void StartRunning(float filler)
        {
            RockAI.StartRunning();
        }
    }
}
