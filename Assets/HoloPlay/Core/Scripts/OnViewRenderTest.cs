//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System.Collections;
using System.Collections.Generic;

using HoloPlay;

using UnityEngine;

namespace HoloPlay
{
    namespace Extras
    {
        public class OnViewRenderTest : MonoBehaviour
        {
            public Quaternion initialRotation;
            public float rotationAmount = 90;


            //* example: how to use onViewRender */
            //Make sure to subscribe when enabled and unsubscribe to prevent memory leaks
            void OnEnable()
            {
                Quilt.onViewRender += FlipCubeOnView;
            }

            void OnDisable()
            {
                Quilt.onViewRender -= FlipCubeOnView;
            }


            void Start()
            {
                initialRotation = transform.rotation;
            }

            void FlipCubeOnView(int viewIndex, int numViews)
            {
                transform.rotation = initialRotation * Quaternion.Euler(0, rotationAmount * viewIndex / numViews, 0);
            }
        }
    }
}