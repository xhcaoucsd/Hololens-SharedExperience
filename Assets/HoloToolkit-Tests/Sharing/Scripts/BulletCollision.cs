using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

namespace HoloToolkit.Sharing.Tests
{

    public class BulletCollision : MonoBehaviour
    {

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.tag == "player")
            {
                long playerID = collision.gameObject.gameObject.GetComponent<Player>().PlayerID;
                if (GameManager.Instance.IDisMine(playerID))
                {
                    AudioManager.Instance.PlayClip("player" + playerID.ToString(), (int)AudioManager.AudioClips.Dink, true);
                }

            }
        }

        private void Start()
        {
            Destroy(gameObject, 10.0f);
        }
    }
}
        

    