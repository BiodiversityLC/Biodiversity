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

        private int stepSoundIndex = 0;

        private int runSoundIndex = 0;

        // Ik it says anim handler but this script is on the right object for sound events.
        public void FootStep()
        {
            if (mainAI.running)
            {
                AudioSource.PlayOneShot(mainAI.runSounds[runSoundIndex]);

                runSoundIndex++;
                if (runSoundIndex > mainAI.runSounds.Length - 1)
                {
                    runSoundIndex = 0;
                }
            } else
            {
                try
                {
                    AudioSource.PlayOneShot(mainAI.stepSounds[stepSoundIndex]);
                } catch (NullReferenceException e)
                {
                    bool AI = mainAI == null;
                    BiodiversityPlugin.LogVerbose($"Caught null on MicBird step sounds. This is caused by a mod incompatibility but can be ignored. ({mainAI == null}, {(AI ? (mainAI.stepSounds == null) : false)})");
                }

                stepSoundIndex++;
                if (stepSoundIndex > mainAI.stepSounds.Length - 1)
                {
                    stepSoundIndex = 0;
                }
            }
        }

        public void Sing()
        {
            mainAI.creatureVoice.PlayOneShot(mainAI.singSounds[Random.Range(0, mainAI.singSounds.Length)]);
        }

        public void EndHurt(string s)
        {
            BiodiversityPlugin.Logger.LogInfo(s);

            mainAI.EndHurt();
        }
    }
}
