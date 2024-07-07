using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Biodiversity.Creatures.Aloe.CustomStateMachineBehaviours;

public class BaseStateMachineBehaviour : StateMachineBehaviour
{
    protected ManualLogSource Mls;
    protected string AloeId;
    
    protected AloeNetcodeController NetcodeController;

    protected void OnEnable()
    {
        if (NetcodeController == null) return;
        NetcodeController.OnSyncAloeId += HandleSyncAloeId;
    }

    protected void OnDisable()
    {
        if (NetcodeController == null) return;
        NetcodeController.OnSyncAloeId -= HandleSyncAloeId;
    }

    public void Initialize(AloeNetcodeController receivedNetcodeController)
    {
        NetcodeController = receivedNetcodeController;
        OnEnable();
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
    }

    private void HandleSyncAloeId(string receivedAloeId)
    {
        AloeId = receivedAloeId;
        Mls?.Dispose();
        Mls = Logger.CreateLogSource(
            $"Aloe Base State Behaviour {AloeId}");
        
        LogDebug("Successfully synced aloe identifier");
    }
    
    protected void LogDebug(string msg)
    {
#if DEBUG
        Mls?.LogInfo(msg);
#endif
    }
}