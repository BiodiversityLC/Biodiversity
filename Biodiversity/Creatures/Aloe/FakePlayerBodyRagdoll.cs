using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

public class FakePlayerBodyRagdoll : MonoBehaviour
{
    /// <summary>
    /// This enum is for the vanilla DeadPlayerInfo body parts variable
    /// </summary>
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

#pragma warning disable 0649
    [SerializeField] private SkinnedMeshRenderer bodyMeshRenderer;
#pragma warning restore 0649

    public List<BodyPart> bodyParts = [];

    private Vector3 _forceDirection;

    public float maxVelocity = 0.4f;
    public float speedMultiplier = 14f;

    public bool matchPositionExactly = true;
    public bool lerpBeforeMatchingPosition;

    public float resetInterval = 0.25f;
    public float lerpDuration = 0.3f;
    public float forceMinDistance = 0.2f;
    public float forceMaxDistance = 2.5f;
    public float maxDistanceToReset = 4.0f;

    private bool _wasMatchingPosition;

    private float _moveToExactPositionTimer;
    private float _restBodyPartsTimer;

    private void Update()
    {
        foreach (BodyPart bodyPart in bodyParts.Where(bodyPart => bodyPart.active))
        {
            if (bodyPart.attachedTo != null && matchPositionExactly)
                ResetBodyPositionIfTooFarFromAttachment(bodyPart);

            _restBodyPartsTimer += Time.deltaTime;
            if (_restBodyPartsTimer >= resetInterval)
            {
                _restBodyPartsTimer = 0f;
                EnableCollisionOnBodyParts();
            }
        }
    }

    private void LateUpdate()
    {
        foreach (BodyPart bodyPart in bodyParts.Where(bodyPart => bodyPart.active))
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
        Debug.Log($"Handling detached limb {bodyPart.name}");
        _wasMatchingPosition = false;

        bodyPart.limbRigidbody.velocity = Vector3.zero;
        bodyPart.limbRigidbody.angularVelocity = Vector3.zero;
        bodyPart.limbRigidbody.ResetCenterOfMass();
        bodyPart.limbRigidbody.ResetInertiaTensor();
        bodyPart.limbRigidbody.freezeRotation = false;
        bodyPart.limbRigidbody.isKinematic = false;

        EnableCollisionOnBodyParts();
    }

    private void HandleAttachedLimb(BodyPart bodyPart)
    {
        Debug.Log($"Handling attached limb {bodyPart.name}");
        if (matchPositionExactly)
        {
            if (lerpBeforeMatchingPosition && _moveToExactPositionTimer < lerpDuration)
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

                bodyPart.limbRigidbody.velocity = Vector3.zero;
                bodyPart.limbRigidbody.angularVelocity = Vector3.zero;
                bodyPart.limbRigidbody.position = bodyPart.attachedTo.position;
                bodyPart.limbRigidbody.rotation = bodyPart.attachedTo.rotation;
                bodyPart.limbRigidbody.centerOfMass = Vector3.zero;
                bodyPart.limbRigidbody.inertiaTensorRotation = Quaternion.identity;
                return;
            }
        }

        ApplyForcesToLimb(bodyPart.limbRigidbody, bodyPart.attachedTo);
    }

    private void MatchPositionExactly(BodyPart bodyPart)
    {
        Debug.Log($"Matching position exactly for {bodyPart.name}");
        _wasMatchingPosition = true;
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
        _forceDirection = (target.position - limb.position).normalized;
        float distance = Vector3.Distance(target.position, limb.position);
        
        if (distance < forceMinDistance)
        {
            limb.velocity = Vector3.zero;
            return;
        }

        limb.AddForce(
            _forceDirection * (speedMultiplier * Mathf.Clamp(Vector3.Distance(target.position, limb.position),
                forceMinDistance, forceMaxDistance)),
            ForceMode.VelocityChange);

        if (limb.velocity.sqrMagnitude > maxVelocity)
        {
            limb.velocity = limb.velocity.normalized * maxVelocity;
        }
    }

    private void ResetBodyPositionIfTooFarFromAttachment(BodyPart activeBodyPart)
    {
        foreach (BodyPart bodyPart in bodyParts.Where(
                     bodyPart => Vector3.Distance(
                                     bodyPart.limbRigidbody.position,
                                     activeBodyPart.attachedTo.position) >
                                 maxDistanceToReset))
        {
            Debug.Log($"Resetting body position for limb {bodyPart.name}");
            _restBodyPartsTimer = 0f;
            bodyPart.limbCollider.enabled = false;
        }
    }

    private void EnableCollisionOnBodyParts()
    {
        Debug.Log("Enabling collision on all body parts");
        foreach (BodyPart bodyPart in bodyParts)
        {
            bodyPart.limbCollider.enabled = true;
        }
    }

    public void ApplySuitMaterial(int playerObjectId)
    {
        if (StartOfRound.Instance == null) return;

        PlayerControllerB playerScript = StartOfRound.Instance.allPlayerScripts[playerObjectId];
        bodyMeshRenderer.sharedMaterial =
            StartOfRound.Instance.unlockablesList.unlockables[playerScript.currentSuitID].suitMaterial;
        bodyMeshRenderer.renderingLayerMask = (uint)(513 | 1 << playerObjectId + 12);
    }

    // public void SetRagdollPositionSafely(Vector3 newPosition, bool disableSpecialEffects = false)
    // {
    //     transform.position = newPosition + Vector3.up * 2.5f;
    //     foreach (Rigidbody bodyPart in bodyParts)
    //     {
    //         bodyPart.velocity = Vector3.zero;
    //     }
    // }
    //
}