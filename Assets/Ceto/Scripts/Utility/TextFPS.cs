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

        private Text text;
        private Button button;

        private void Awake()
        {
            text = GetComponent<Text>();
            button = GetComponent<Button>();
            button.onClick.AddListener(() => { text.enabled = !text.enabled; });
        }

        // Update is called once per frame
        void Update()
        {
            text.text = "FPS = " + m_fps.FrameRate.ToString("F2");
        }
    }
}

