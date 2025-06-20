using UnityEngine;

namespace Biodiversity.Items.DeveloperItems;

public class IronDogBehaviour : BiodiverseItem
{
    //todo: add config for this
    public float bigShakeMaxDistance = 3f;
    public float smallShakeMaxDistance = 6f;
    
    public override void OnHitGround()
    {
        base.OnHitGround();
        
        float localPlayerDistanceToDog = Vector3.Distance(transform.position, HUDManager.Instance.localPlayer.transform.position);
        
        if (localPlayerDistanceToDog <= bigShakeMaxDistance) 
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
        }
        else if (localPlayerDistanceToDog <= smallShakeMaxDistance)
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
        }
    }
}