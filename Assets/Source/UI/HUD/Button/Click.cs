using UnityEngine;

namespace HUD {
    public class Click : MonoBehaviour {
        public StateEvent<int> stateToUpdate;
        public void WhenClicked() {
            stateToUpdate.Set(stateToUpdate.Get() + 1);
        }
    }
}