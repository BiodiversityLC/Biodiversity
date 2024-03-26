using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.General;
public abstract class BiodiverseAI<T> where T : BiodiverseAI<T> {
    protected void InitAI(EnemyState<T> defaultState) {
        _currentState = defaultState;
    }

    public EnemyState<T> CurrentState { 
        get {
            return _currentState;
        }
        set {
            _currentState.OnExitState();
            value.OnEnterState();
            BiodiversityPlugin.Logger.LogInfo($"[{GetName()}] Switching state to {GetName()}.{value.Name}");
            _currentState = value;  
        }
    }
    public abstract string GetName();
    
    private EnemyState<T> _currentState;
}
