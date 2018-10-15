using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloToolkit.Sharing.Tests
{
    public class PlayerCapsuleMorph : MonoBehaviour
    {

        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "player")
            {
                if (GameManager.Instance.IDisMine(other.GetComponent<Player>().PlayerID))
                {
                    long playerID = other.gameObject.GetComponent<Player>().PlayerID;
                    ObjectManager.Instance.ChangeMesh("player" + playerID, (int)ObjectManager.Meshes.Capsule, true);
                    AudioManager.Instance.PlayClip("player" + playerID, (int)AudioManager.AudioClips.Pop, true);
                }

            }
        }
    }
}