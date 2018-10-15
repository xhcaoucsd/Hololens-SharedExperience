using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloToolkit.Sharing.Tests
{

    public class Player : MonoBehaviour
    {

        public long PlayerID
        {
            get; set;
        }

        private bool emissionDisabled = false;
        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (!emissionDisabled && GameManager.Instance.IDisMine(PlayerID))
            {
                ParticleSystem ps = GetComponent<ParticleSystem>();
                if (ps)
                {
                    ParticleSystem.EmissionModule em = ps.emission;
                    em.enabled = false;
                    emissionDisabled = true;
                }
            }
        }


    }
}
