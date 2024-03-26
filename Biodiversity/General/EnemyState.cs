using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.General;
public abstract class EnemyState<T>(T AI, string Name) where T : BiodiverseAI<T> {
    public T AI { get; private set; } = AI;

    public string Name { get; private set; } = Name;

    public virtual void OnEnterState() { }
    public virtual void OnExitState() { }
    public virtual void OnAIInterval() { }
}
