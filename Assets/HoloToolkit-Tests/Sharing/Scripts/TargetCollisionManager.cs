using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

namespace HoloToolkit.Sharing.Tests
{
    public class TargetCollisionManager : MonoBehaviour
    {
            
        public Material materialTargetCollided;

        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "player")
            {
                if (GameManager.Instance.ImGameMaster())
                {
                    long playerID = other.gameObject.GetComponent<Player>().PlayerID;
                    Debug.Log("HELLO");
                    GameManager.Instance.TargetCollision(playerID, broadcast: true);

                }
            }
        }
    }
}
