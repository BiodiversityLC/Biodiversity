using UnityEngine;
using UnityEngine.Events;

namespace Biodiversity.Behaviours;

public class AnimationFunctionEvent : MonoBehaviour {
	[SerializeField]
	UnityEvent OnInvoke;

	public void Invoke() {
		OnInvoke.Invoke();
	}
}