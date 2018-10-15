
using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

namespace HoloToolkit.Sharing.Tests
{
    /// <summary>
    /// Broadcasts the head transform of the local user to other users in the session,
    /// and adds and updates the head transforms of remote users.
    /// Head transforms are sent and received in the local coordinate space of the GameObject this component is on.
    /// </summary>
    public class ObjectManager : Singleton<ObjectManager>
    {
        public enum Prefabs : int
        {
            Player,
            Target,
            Wardrobe,
            Bullet,
            Wardrobe2,
            Gallery
        }

        public enum Materials : int
        {
            Normal,
            GM,
            Dead,
            Alive,
            Collided
        }

        public enum Meshes : int
        {
            Cube,
            Sphere,
            Capsule
        }

        public List<GameObject> prefabs;
        public List<Material> materials;
        public List<Mesh> meshes;

        public Dictionary<string, GameObject> sharedObjects = new Dictionary<string, GameObject>();

        public bool Started { get; private set; }


        private void Start()
        {
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.SpawnObject] = HandleSpawnObject;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.DestroyObject] = HandleDestroyObject;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.PosObject] = HandlePosObject;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.RotObject] = HandleRotObject;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ScaleObject] = HandleScaleObject;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ApplyVelocity] = HandleApplyVelocity;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ChangeMaterial] = HandleChangeMaterial;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.LoadMaterial] = HandleLoadMaterial;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.ChangeMesh] = HandleChangeMesh;
            CustomMessages.Instance.MessageHandlers[CustomMessages.TestMessageID.SetActive] = HandleSetActive;

            // SharingStage should be valid at this point, but we may not be connected.
            if (SharingStage.Instance.IsConnected)
            {
                Connected();
            }
            else
            {
                SharingStage.Instance.SharingManagerConnected += Connected;
            }
            Started = true;
        }

        public void Initialize()
        {
            sharedObjects.Add("camera_main", Camera.main.gameObject);
        }

        private void Connected(object sender = null, EventArgs e = null)
        {
            SharingStage.Instance.SharingManagerConnected -= Connected;
        }
        
        // Spawns object relative to world anchor
        public void SpawnObject(string objectID, int prefab, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendSpawnObject(objectID, prefab);

            sharedObjects.Add(objectID, Instantiate(prefabs[prefab], transform));
        }

        public void DestroyObject(string objectID, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendDestroyObject(objectID);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
            {
                sharedObjects.Remove(objectID);
                Destroy(currentObject);
            }
        }

        public void PosObject(string objectID, Vector3 position, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendPosObject(objectID, position);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
                currentObject.transform.localPosition = position;
            
        }

        public void RotObject(string objectID, Quaternion rotation, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendRotObject(objectID, rotation);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
                currentObject.transform.localRotation = rotation;
        }

        public void ScaleObject(string objectID, Vector3 scale, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendScaleObject(objectID, scale);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
                currentObject.transform.localScale = scale;
        }

        public void ApplyVelocity(string objectID, Vector3 velocity, bool broadcast = false)
        {
            if (broadcast)
                CustomMessages.Instance.SendApplyVelocity(objectID, velocity);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject)
                && currentObject.GetComponent<Rigidbody>()) 
                currentObject.GetComponent<Rigidbody>().velocity = transform.TransformPoint(velocity) - transform.position;
        }

        public void ApplyAngVelocity(string objectID, Vector3 angvelocity, bool broadcast = false)
        {
            if (broadcast)
                CustomMessages.Instance.SendApplyAngVelocity(objectID, angvelocity);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject)
                && currentObject.GetComponent<Rigidbody>())
                currentObject.GetComponent<Rigidbody>().angularVelocity = angvelocity;
        }

        public void ChangeMaterial(string objectID, int mat, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendChangeMaterial(objectID, mat);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
            {   
                if (currentObject.GetComponent<Renderer>())
                    currentObject.GetComponent<Renderer>().sharedMaterial = materials[mat];
                else if (currentObject.GetComponentInChildren<Renderer>())
                    currentObject.GetComponentInChildren<Renderer>().sharedMaterial = materials[mat];
            }
        }

        public void LoadMaterial(string objectID, string mat, bool broadcast = false)
        {
            if (broadcast)
                CustomMessages.Instance.SendLoadMaterial(objectID, mat);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
            {
                if (currentObject.GetComponent<Renderer>())
                    currentObject.GetComponent<Renderer>().sharedMaterial = Resources.Load(mat, typeof(Material)) as Material;
                else if (currentObject.GetComponentInChildren<Renderer>())
                    currentObject.GetComponentInChildren<Renderer>().sharedMaterial = Resources.Load(mat, typeof(Material)) as Material;
            }
        }

        public void ChangeMesh(string objectID, int mesh, bool broadcast=false)
        {
            if (broadcast)
                CustomMessages.Instance.SendChangeMesh(objectID, mesh);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
            {
                if (currentObject.GetComponent<MeshFilter>())
                    currentObject.GetComponent<MeshFilter>().sharedMesh = meshes[mesh];
                else if (currentObject.GetComponentInChildren<MeshFilter>())
                    currentObject.GetComponentInChildren<MeshFilter>().sharedMesh = meshes[mesh];
            }
        }

        public void SetActive(string objectID, int active, bool broadcast = false)
        {
            if (broadcast)
                CustomMessages.Instance.SendSetActive(objectID, active);

            GameObject currentObject;
            if (sharedObjects.TryGetValue(objectID, out currentObject))
            {
                if (active == 0)
                    currentObject.SetActive(false);
                else
                    currentObject.SetActive(true);
            }
        }

        public void HandleSpawnObject(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            int prefab = msg.ReadInt32();

            SpawnObject(objectID, prefab);
        }

        public void HandleDestroyObject(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();

            DestroyObject(objectID);
        }

        public void HandlePosObject(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            Vector3 position = CustomMessages.Instance.ReadVector3(msg);

            PosObject(objectID, position);
        }

        public void HandleRotObject(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            Quaternion rotation = CustomMessages.Instance.ReadQuaternion(msg);

            RotObject(objectID, rotation);
        }

        public void HandleApplyVelocity(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            Vector3 velocity = CustomMessages.Instance.ReadVector3(msg);

            ApplyVelocity(objectID, velocity);
        }

        public void HandleApplyAngVelocity(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            Vector3 angvelocity = CustomMessages.Instance.ReadVector3(msg);

            ApplyAngVelocity(objectID, angvelocity);
        }

        public void HandleScaleObject(NetworkInMessage msg)
        {
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            Vector3 scale = CustomMessages.Instance.ReadVector3(msg);

            ScaleObject(objectID, scale);
        }

        private void HandleChangeMaterial(NetworkInMessage msg)
        {
            // Parse the message
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            int mat = msg.ReadInt32();

            ChangeMaterial(objectID, mat);
        }

        private void HandleLoadMaterial(NetworkInMessage msg)
        {
            // Parse the message
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            string mat = msg.ReadString().ToString();

            LoadMaterial(objectID, mat);
        }

        private void HandleChangeMesh(NetworkInMessage msg)
        {
            // Parse the message
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            int mesh = msg.ReadInt32();

            ChangeMesh(objectID, mesh);
        }

        private void HandleSetActive(NetworkInMessage msg)
        {
            // Parse the message
            long senderID = msg.ReadInt64();
            string objectID = msg.ReadString().ToString();
            int active = msg.ReadInt32();

            SetActive(objectID, active);
        }


    }
}