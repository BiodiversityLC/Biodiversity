using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAnimHandler : MonoBehaviour
    {
        public MicBirdAI mainAI;

        public AudioSource AudioSource;

        public void FootStep()
        {
            if (mainAI.running)
            {
                AudioSource.PlayOneShot(mainAI.runSounds[Random.RandomRangeInt(0, mainAI.runSounds.Length)]);
            } else
            {
                AudioSource.PlayOneShot(mainAI.stepSounds[Random.RandomRangeInt(0, mainAI.stepSounds.Length)]);
            }
        }

        public void EndHurt(string s)
        {
            BiodiversityPlugin.Logger.LogInfo(s);

            mainAI.EndHurt();
        }
    }
}
