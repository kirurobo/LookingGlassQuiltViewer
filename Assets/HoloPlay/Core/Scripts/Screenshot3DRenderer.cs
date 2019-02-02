//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloPlay
{
    public class Screenshot3DRenderer : MonoBehaviour
    {
        public Renderer r;
        public int tilesX;
        public int tilesY;

        void OnEnable() { Quilt.onViewRender += ShowScene; }
        void OnDisable() { Quilt.onViewRender -= ShowScene; }

        // todo: shader needs to be fixed
        void Awake()
        {
            r = GetComponent<Renderer>();
            r.material.SetFloat("_TilesX", tilesX);
            r.material.SetFloat("_TilesY", tilesY);
        }

        void OnValidate()
        {
            r.material.SetFloat("_TilesX", tilesX);
            r.material.SetFloat("_TilesY", tilesY);
        }

        void ShowScene(int i, int numViews)
        {
            int j = i;
            // ? this might be broken on holoplayers, gotta check
            r.material.SetFloat("_View", j);
        }
    }
}