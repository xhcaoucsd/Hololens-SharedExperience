using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;

namespace HoloToolkit.Sharing.Tests
{

    public class EdgeLordController : ModelController
    {
        public float durationTime = 1.0f;

        public AudioClip clipBGM;
        public AudioClip clipIdle;
        public AudioClip clipActive;

        private bool rollover;
        private float duration;

        // Use this for initialization
        void Start()
        {
            InputManager.Instance.PushModalInputHandler(gameObject);
            rollover = false;
            duration = 0f;
        }

        void Update()
        {
            if (Time.time > duration)
                rollover = false;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator)
                animator.SetBool("Rollover", rollover);


            AudioSource source = GetComponent<AudioSource>();
            if (!source.isPlaying)
            {
                source.clip = clipIdle;
                source.Play();
            }
        }

        public override void Rollover()
        {
            rollover = true;
            duration = durationTime + Time.time;
        }

        public override void Activate()
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator)
                animator.SetTrigger("PlayAnim");

            AudioSource source = GetComponent<AudioSource>();
            if (!source.isPlaying || source.clip != clipActive)
            {
                source.clip = clipActive;
                source.Play();
            }

        }
    }
}
