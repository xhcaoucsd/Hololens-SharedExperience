using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GalleryManager : MonoBehaviour {

    private readonly float DEGREES_360 = 360.0f;

    public List<GameObject> models = new List<GameObject>();
    public float seperationDistance = 1.0f;
    public float minSeperationDistance = 1.0f;
	// Use this for initialization
	void Start () {
        
    }
	
	// Update is called once per frame
	void Update () {
        /*
        float radiansPerModel = 2 * Mathf.PI / models.Count;
        float radius = seperationDistance / radiansPerModel;

        for (int i = 0; i < models.Count; i++)
        {
            float angle = 2 * Mathf.PI / models.Count * i;
            models[i].transform.localPosition = new Vector3(radius * Mathf.Cos(angle), 0.0f, radius * Mathf.Sin(angle));
            models[i].transform.localRotation = Quaternion.Euler(0.0f, -90.0f - Mathf.Rad2Deg * angle, 0.0f);
        }*/

    }
}
