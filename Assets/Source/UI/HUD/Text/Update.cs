using System;
using TMPro;
using UnityEngine;

namespace HUD {
    public class Update : MonoBehaviour {
        public StateEvent<int> state;
        private TextMeshProUGUI text;

        private void OnEnable() {
            text = GetComponent<TextMeshProUGUI>();
            state += updateText;
        }
        
        private void OnDisable() {
            state -= updateText;
        }

        private void updateText(int value) {
            text.text = value.ToString();
        }
    }
}