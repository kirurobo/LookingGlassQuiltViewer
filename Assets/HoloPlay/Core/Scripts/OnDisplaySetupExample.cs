using UnityEngine;
using UnityEngine.UI;

namespace HoloPlay
{
    namespace Extras
    {
        public class OnDisplaySetupExample : MonoBehaviour
        {
            public void OnDisplaySetup(bool secondScreen)
            {
                var text = GetComponent<Text>();

                if (secondScreen)
                {
                    text.text += "<color=green> • UI Second Screen :)</color>";
                }
                else
                {
                    text.text += "<color=yellow> • UI Single Screen</color>";
                }
            }
        }
    }
}