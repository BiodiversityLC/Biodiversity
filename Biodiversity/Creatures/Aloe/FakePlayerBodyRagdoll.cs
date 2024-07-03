using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

public class FakePlayerBodyRagdoll : MonoBehaviour
{
    public enum DeadPlayerBodyParts
    {
        Neck = 0,
        LowerRightArm = 1,
        LowerLeftArm = 2,
        RightShin = 3,
        LeftShin = 4,
        Torso = 5,
        Root = 6,
        RightThigh = 7,
        LeftThigh = 8,
        UpperLeftArm = 9,
        UpperRightArm = 10,
    }

    [SerializeField] private SkinnedMeshRenderer bodyMeshRenderer;

    public List<BodyPart> bodyParts = [];
    
    public float maxVelocity = 10f;
    public float speedMultiplier = 1f;
    
    public bool matchPositionExactly = true;
    public bool lerpBeforeMatchingPosition;

    private const float ResetInterval = 0.25f;
    private const float LerpDuration = 0.3f;
    private const float ForceMinDistance = 0.2f;
    private const float ForceMaxDistance = 2.5f;
    private const float MaxDistanceToReset = 4.0f;

    private Vector3 _forceDirection;
    
    private bool _wasMatchingPosition;
    
    private float _moveToExactPositionTimer;

    private void Update()
    {
        foreach (BodyPart bodyPart in bodyParts.Where(bodyPart => bodyPart.attachedTo != null && matchPositionExactly))
        {
            ResetBodyPositionIfTooFarFromAttachment(bodyPart);
        }
    }

    private void LateUpdate()
    {
        foreach (BodyPart bodyPart in bodyParts)
        {
            if (bodyPart.attachedTo == null || bodyPart.attachedTo.parent == transform)
            {
                HandleDetachedLimb(bodyPart);
            }
            else
            {
                HandleAttachedLimb(bodyPart);
            }
        }
    }

    private void HandleDetachedLimb(BodyPart bodyPart)
    {
        _moveToExactPositionTimer = 0.0f;
        if (!_wasMatchingPosition) return;

        _wasMatchingPosition = false;
        ResetLimbProperties(bodyPart.limbRigidbody);
        EnableCollisionOnBodyParts();
    }

    private void HandleAttachedLimb(BodyPart bodyPart)
    {
        if (matchPositionExactly)
        {
            if (lerpBeforeMatchingPosition && _moveToExactPositionTimer < LerpDuration)
            {
                _moveToExactPositionTimer += Time.deltaTime;
                speedMultiplier = 25f;
            }
            else
            {
                if (!_wasMatchingPosition)
                {
                    MatchPositionExactly(bodyPart);
                    return;
                }

                bodyPart.limbRigidbody.position = bodyPart.attachedTo.position;
                bodyPart.limbRigidbody.rotation = bodyPart.attachedTo.rotation;
                bodyPart.limbRigidbody.centerOfMass = Vector3.zero;
                bodyPart.limbRigidbody.inertiaTensorRotation = Quaternion.identity;
            }
        }

        ApplyForcesToLimb(bodyPart.limbRigidbody, bodyPart.attachedTo);
    }

    private void MatchPositionExactly(BodyPart bodyPart)
    {
        _wasMatchingPosition = true;
        Vector3 offset = transform.position - bodyPart.limbRigidbody.position;
        transform.GetComponent<Rigidbody>().position = bodyPart.attachedTo.position + offset;
        bodyPart.limbRigidbody.freezeRotation = true;
        bodyPart.limbRigidbody.isKinematic = true;
        bodyPart.limbRigidbody.transform.position = bodyPart.attachedTo.position;
        bodyPart.limbRigidbody.transform.rotation = bodyPart.attachedTo.rotation;

        foreach (BodyPart otherBodyPart in bodyParts)
        {
            otherBodyPart.limbRigidbody.angularDrag = 1f;
            otherBodyPart.limbRigidbody.maxAngularVelocity = 2f;
            otherBodyPart.limbRigidbody.maxDepenetrationVelocity = 0.3f;
            otherBodyPart.limbRigidbody.velocity = Vector3.zero;
            otherBodyPart.limbRigidbody.angularVelocity = Vector3.zero;
            otherBodyPart.limbRigidbody.WakeUp();
        }
    }

    private void ApplyForcesToLimb(Rigidbody limb, Transform target)
    {
        _forceDirection = Vector3.Normalize(target.position - limb.position);
        limb.AddForce(
            _forceDirection * (speedMultiplier * Mathf.Clamp(Vector3.Distance(target.position, limb.position), ForceMinDistance, ForceMaxDistance)),
            ForceMode.VelocityChange);

        if (limb.velocity.sqrMagnitude > maxVelocity * maxVelocity)
        {
            limb.velocity = limb.velocity.normalized * maxVelocity;
        }
    }

    private static void ResetLimbProperties(Rigidbody limb)
    {
        limb.ResetCenterOfMass();
        limb.ResetInertiaTensor();
        limb.freezeRotation = false;
        limb.isKinematic = false;
    }

    private void ResetBodyPositionIfTooFarFromAttachment(BodyPart bodyPart)
    {
        if (!(Vector3.Distance(bodyPart.limbRigidbody.position, bodyPart.attachedTo.position) >
              MaxDistanceToReset)) return;
        
        bodyPart.limbCollider.enabled = false;
        Invoke(nameof(EnableCollisionOnBodyParts), ResetInterval);

    }

    public void ApplySuitMaterial(int playerObjectId)
    {
        if (StartOfRound.Instance == null) return;
        
        PlayerControllerB playerScript = StartOfRound.Instance.allPlayerScripts[playerObjectId];
        bodyMeshRenderer.sharedMaterial =
            StartOfRound.Instance.unlockablesList.unlockables[playerScript.currentSuitID].suitMaterial;
        bodyMeshRenderer.renderingLayerMask = (uint) (513 | 1 << playerObjectId + 12);
    }

    private void EnableCollisionOnBodyParts()
    {
        foreach (BodyPart bodyPart in bodyParts)
        {
            bodyPart.limbCollider.enabled = true;
        }
    }

    // public void ResetRagdollPosition()
    // {
    //     transform.position = attachedLimb != null && attachedTo != null ? attachedTo.position + Vector3.up * 2f : _spawnPosition;
    //     foreach (Rigidbody bodyPart in bodyParts)
    //     {
    //         bodyPart.velocity = Vector3.zero;
    //         bodyPart.GetComponent<Collider>().enabled = false;
    //     }
    // }
    //
    // public void SetRagdollPositionSafely(Vector3 newPosition, bool disableSpecialEffects = false)
    // {
    //     transform.position = newPosition + Vector3.up * 2.5f;
    //     foreach (Rigidbody bodyPart in bodyParts)
    //     {
    //         bodyPart.velocity = Vector3.zero;
    //     }
    // }
    //
    // public void AddForceToBodyPart(int bodyPartIndex, Vector3 force)
    // {
    //     if (bodyPartIndex >= 0 && bodyPartIndex < bodyParts.Length)
    //     {
    //         bodyParts[bodyPartIndex].AddForce(force, ForceMode.Impulse);
    //     }
    // }
}
