using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomCursor : MonoBehaviour {
    public Texture2D cursorTexture;
    public Vector2 hotSpot;

    // Use this for initialization
    void Start () {
        if (cursorTexture)
        {
            Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.ForceSoftware);
        }
    }
    
    // Update is called once per frame
    void Update () {
        
    }
}
