using Biodiversity.Creatures.ClockworkAngel.Scripts;
using Biodiversity.Util.Attributes;
using HarmonyLib;
using System;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

namespace Biodiversity.Creatures.ClockworkAngel.Patches
{
    [CreaturePatch("ClockworkAngel")]
    [HarmonyPatch(typeof(TimeOfDay))]
    internal static class ClockworkAngelTimeOfDayPatch
    {
        [HarmonyPatch(nameof(TimeOfDay.DecideRandomDayEvents)), HarmonyPostfix]
        internal static void AngelTimeCalc()
        {
            if (!TimeOfDay.Instance.IsServer)
                return;


            Random random = new Random(StartOfRound.Instance.randomMapSeed + 28);

            if (random.Next(0, 1) == 0)
            {
                ClockworkSharedData.AngelAppears = true;
                BiodiversityPlugin.LogVerbose("The angel will appear today");
            }
        }

        [HarmonyPatch(nameof(TimeOfDay.MoveTimeOfDay)), HarmonyPostfix]
        internal static void AngelSpawnCheck()
        {
            if (TimeOfDay.Instance.IsServer && ClockworkSharedData.AngelAppears && TimeOfDay.Instance.normalizedTimeOfDay >= 0.2)
            {
                ClockworkSharedData.AngelAppears = false;
                GameObject angel = GameObject.Instantiate(ClockworkAngelHandler.Instance.Assets.enemy, new Vector3(0, 75, 0), Quaternion.identity);
                angel.GetComponent<NetworkObject>().Spawn();

                GameObject agent = GameObject.Instantiate(ClockworkAngelHandler.Instance.Assets.AngelAgent, new Vector3(0, 0, 0), Quaternion.identity);
                GameObject spotlight = GameObject.Instantiate(ClockworkAngelHandler.Instance.Assets.AngelSpotlight, new Vector3(0, 0, 0), Quaternion.identity);
                agent.GetComponent<NetworkObject>().Spawn();
                spotlight.GetComponent<NetworkObject>().Spawn();

                ClockworkAngelAI AI = angel.GetComponent<ClockworkAngelAI>();
                AngelAgent agentScript = agent.GetComponent<AngelAgent>();
                AngelSpotlight spotlightScript = spotlight.GetComponent<AngelSpotlight>();

                AI.AngelSpotlight = spotlightScript;
                AI.AngelAgent = agentScript;

                spotlightScript.ParentAngel = AI;
            }
        }
    }
}
