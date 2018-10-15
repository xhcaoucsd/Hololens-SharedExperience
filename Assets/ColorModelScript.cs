using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloToolkit.Sharing.Tests
{
    public class ColorModelScript : MonoBehaviour
    {
        public string matID;

        private void Start()
        {
            gameObject.GetComponent<Renderer>().material = Resources.Load(matID, typeof(Material)) as Material;
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "player")
            {
                if (GameManager.Instance.IDisMine(other.GetComponent<Player>().PlayerID))
                {
                    long playerID = other.gameObject.GetComponent<Player>().PlayerID;
                    ObjectManager.Instance.LoadMaterial("player" + playerID, matID, true);

                    //ObjectManager.Instance.ChangeMaterial("player" + playerID, (int)ObjectManager.Materials.Dead, true);
                    AudioManager.Instance.PlayClip("player" + playerID, (int)AudioManager.AudioClips.Pop, true);
                }

            }
        }
    }
}