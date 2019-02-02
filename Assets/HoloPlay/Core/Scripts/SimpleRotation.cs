//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using UnityEngine;

namespace HoloPlay
{
    namespace Extras
    {
        public class SimpleRotation : MonoBehaviour
        {
            public Vector3 rotation;
            public Space space;
            public bool timeIndependent;

            void Update()
            {
                float d = timeIndependent ? Time.deltaTime : 1;
                transform.Rotate(rotation * d, space);
            }
        }
    }
}