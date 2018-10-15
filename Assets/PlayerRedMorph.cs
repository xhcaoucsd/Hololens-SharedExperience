using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloToolkit.Sharing.Tests
{
    public class PlayerRedMorph : MonoBehaviour
    {

        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "player")
            {
                if (GameManager.Instance.IDisMine(other.GetComponent<Player>().PlayerID))
                {
                    long playerID = other.gameObject.GetComponent<Player>().PlayerID;
                    ObjectManager.Instance.ChangeMaterial("player" + playerID, (int)ObjectManager.Materials.Dead, true);
                    AudioManager.Instance.PlayClip("player" + playerID, (int)AudioManager.AudioClips.Pop, true);
                }

            }
        }
    }
}