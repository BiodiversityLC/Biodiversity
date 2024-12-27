using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAnimHandler : MonoBehaviour
    {
        public MicBirdAI mainAI;



        public void EndHurt(string s)
        {
            BiodiversityPlugin.Logger.LogInfo(s);

            mainAI.EndHurt();
        }
    }
}
