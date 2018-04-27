using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

namespace HoloToolkit.Sharing.Tests
{
    public class TargetCollisionManager : MonoBehaviour
    {
        private GameObject gameManager;
            
        public Material materialTargetCollided;

        // Use this for initialization
        void Start()
        {
            gameManager = GameObject.FindGameObjectWithTag("spawner");
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "player")
            {
                if (gameManager.GetComponent<GameManager>().ImGameMaster())
                {
                    long playerID = other.gameObject.GetComponent<Player>().PlayerID;
                    gameManager.GetComponent<GameManager>().TargetCollision(playerID, broadcast: true);

                }
            }
        }
    }
}
