using Biodiversity.Core.Config;
using Biodiversity.Items.Developeritems;
using UnityEngine;

namespace Biodiversity.Items.DeveloperItems;

public class IronDogBehaviour : BiodiverseItem
{
    public float bigShakeMaxDistance;
    public float smallShakeMaxDistance;

    public override void Start()
    {
        base.Start();

        GenericScrapItem config = DeveloperScrapHandler.Instance.Config.IronDog;
        
        bigShakeMaxDistance = config?.Get<float>("Big Shake Distance") ?? 3f;
        smallShakeMaxDistance = config?.Get<float>("Small Shake Distance") ?? 6f;
    }
    
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