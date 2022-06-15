using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ceto;
using UnityEngine.UI;

namespace Ceto
{
    public class TextFPS : MonoBehaviour
    {
        public Common.Unity.Utility.FPSCounter m_fps;
        public Text text;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            text.text = "Current FPS = " + m_fps.FrameRate.ToString("F2");
        }
    }
}

