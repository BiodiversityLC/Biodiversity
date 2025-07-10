using GameNetcodeStuff;
using UnityEngine;

public class MusketBayonetCollisionDetection : MonoBehaviour
{
    private int spinDamageToPlayers;
    private int stabDamageToPlayers;

    private float spinKnockback;
    private float stabKnockback;
    
    private void Start()
    {
        spinDamageToPlayers = 40;
        stabDamageToPlayers = 50;
        
        spinKnockback = 4f;
        stabKnockback = 2f;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (!player) return;
        }
        else if (other.CompareTag("Enemy"))
        {
            
        }
    }
}