using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.SwarmingLocusts
{
    public class SwarmingLocustsAI : BiodiverseAI
    {
        public AISearchRoutine searchRoutine;
        public GrabbableObject targetItem;

        public static List<GrabbableObject> validGrabbableObjects = [];
        public static float refreshTimer = 0f;
        public readonly float refreshInterval = 100f;

        enum State
        {
            Searching,
            AttackingTarget,
        }

        public override void Start()
        {
            base.Start();
            RefreshValidGrabbableObjects();
            SwitchToBehaviourClientRpc((int)State.Searching);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            }
            switch (currentBehaviourStateIndex)
            {
                case (int)State.Searching:
                    SearchingItem();
                    break;
                case (int)State.AttackingTarget:
                    AttackingItem();
                    break;
                default: break;
            }
        }

        private void SearchingItem()
        {
            agent.speed = 5f;
            if (targetItem == null && !searchRoutine.inProgress)
            {
                StartSearch(transform.position, searchRoutine);
                return;
            }
            if (targetItem != null)
            {
                if (targetItem.isInFactory || targetItem.isInShipRoom || targetItem.isHeld || targetItem.isHeldByEnemy)
                {
                    targetItem = null;
                }
                else if (Vector3.Distance(transform.position, targetItem.transform.position) < 0.75f)
                {
                    SwitchToBehaviourClientRpc((int)State.AttackingTarget);
                }
                else
                {
                    SetGoTowardsTarget();
                }
                return;
            }
            GrabbableObject possibleTarget = GetClosestVisibleItem(validGrabbableObjects, eye, viewWidth: 90, viewRange: 100, onlyOutside: true, ignoreInShip: true, ignoreHeld: true);
            if (possibleTarget != null)
            {
                targetItem = possibleTarget;
                SetGoTowardsTarget();
            }
        }

        private void AttackingItem()
        {
            agent.speed = 0f;
        }

        private void SetGoTowardsTarget()
        {
            if (SetDestinationToPosition(targetItem.transform.position, checkForPath: true))
            {
                LogVerbose($"Found target: {targetItem.itemProperties.itemName}");
                StopSearch(searchRoutine, clear: false);
            }
            else
            {
                targetItem = null;
            }
        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead)
            {
                return;
            }
            refreshTimer += Time.deltaTime;
            if (refreshTimer > refreshInterval)
            {
                RefreshValidGrabbableObjects();
            }
        }

        public static void RefreshValidGrabbableObjects()
        {
            refreshTimer = 0f;
            validGrabbableObjects.Clear();
            GrabbableObject[] allObjects = FindObjectsOfType<GrabbableObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (!allObjects[i].deactivated)
                {
                    validGrabbableObjects.Add(allObjects[i]);
                }
            }
        }
    }
}
