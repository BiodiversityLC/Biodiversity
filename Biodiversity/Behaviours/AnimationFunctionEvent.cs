using UnityEngine;
using UnityEngine.Events;

namespace Biodiversity.Behaviours;

public class AnimationFunctionEvent : MonoBehaviour {
#pragma	warning disable 0649
	[SerializeField] UnityEvent OnInvoke;
#pragma	warning restore 0649

	public void Invoke() {
		OnInvoke.Invoke();
	}
}