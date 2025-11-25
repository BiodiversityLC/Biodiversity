using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Behaviours.Player;

public class PlayerVelocityTracker : MonoBehaviour
{
    public Vector3 Velocity { get; private set; }

    private CachedUnityObject<PlayerControllerB> _playerController;
    private Vector3 _lastPosition;
    private float _timeSinceLastUpdate;

    // todo: make a thingy that lets u toggle these on and off

    private void Awake()
    {
        if (!NetworkManager.Singleton.IsServer || !TryGetComponent(out PlayerControllerB player))
        {
            enabled = false;
            return;

        }

        _playerController.Set(player);
        _lastPosition = _playerController.Value.serverPlayerPosition;
    }

    // Use LateUpdate to ensure we check the position after the game's own LateUpdate has run
    private void LateUpdate()
    {
        if (!_playerController.HasValue) return;

        _timeSinceLastUpdate += Time.deltaTime;

        // Check if the server position has changed since our last check
        if (_playerController.Value.serverPlayerPosition != _lastPosition)
        {
            // A network update has occurred; calculate the velocity
            Vector3 displacement = _playerController.Value.serverPlayerPosition - _lastPosition;

            if (_timeSinceLastUpdate > 0.001f)
            {
                Velocity = displacement / _timeSinceLastUpdate;
            }

            // Reset the stopwatch and update the last known position
            _lastPosition = _playerController.Value.serverPlayerPosition;
            _timeSinceLastUpdate = 0f;
        }
    }
}