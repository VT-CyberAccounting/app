using UnityEngine;

[CreateAssetMenu(fileName = "NewIntEvent", menuName = "AppEvents/StateEvent/Int")]
public class IntStateEvent : StateEvent<int> {}

[CreateAssetMenu(fileName = "NewBoolEvent", menuName = "AppEvents/StateEvent/Bool")]
public class BoolStateEvent : StateEvent<bool> {}

[CreateAssetMenu(fileName = "NewFloatEvent", menuName = "AppEvents/StateEvent/Float")]
public class FloatStateEvent : StateEvent<float> {}