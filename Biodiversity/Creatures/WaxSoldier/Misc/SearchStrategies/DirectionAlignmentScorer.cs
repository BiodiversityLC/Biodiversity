using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Util;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class DirectionAlignmentScorer(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    : UtilityScorer<WaxSoldierBlackboard, WaxSoldierAdapter>(ctx)
{
    private const float MIN_DOT_PRODUCT = 0.707f;
    
    public override float Score(Vector3 candidatePosition, object visualizerOwner)
    {
        Vector3 lkp = context.Blackboard.LastKnownPlayerPosition;
        Vector3 lkv = context.Blackboard.LastKnownPlayerVelocity.normalized;

        if (lkv.sqrMagnitude < 0.1f)
        {
            return 0.5f; // Neutral score if the player was stationary
        }
        
        Vector3 lkvNormalized = lkv.normalized;

        Vector3 directionToNode = (candidatePosition - lkp).normalized;
        float dot = Vector3.Dot(directionToNode, lkvNormalized);
        
        bool isVisualizerOwnerNull = visualizerOwner == null;
        if (!isVisualizerOwnerNull)
        {
            // BiodiversityPlugin.LogVerbose($"[AI Scorer Frame:{visualizerOwner.GetHashCode()}] " +
            //                               $"LKV: {lkv.ToString("F2")}, " +
            //                               $"LKV Normalized: {lkvNormalized.ToString("F2")}, " +
            //                               $"DirToNode: {directionToNode.ToString("F2")}, " +
            //                               $"DOT PRODUCT: {dot:F3}");
            
            // Draw a cyan line representing the player's last known velocity vector
            Vector3 velocityEndPoint = lkp + lkvNormalized * 5f;
            DebugShapeVisualizer.DrawLine(visualizerOwner, lkp, velocityEndPoint, Color.cyan);
            DebugShapeVisualizer.DrawSphere(visualizerOwner, lkp, 0.4f, Color.cyan);
        }

        if (dot < MIN_DOT_PRODUCT)
        {
            return 0f;
        }
        
        float finalScore = (dot - MIN_DOT_PRODUCT) / (1f - MIN_DOT_PRODUCT);

        if (!isVisualizerOwnerNull)
        {
            // Lerp from red (bad score) to green (good score)
            Color scoreColor = Color.Lerp(Color.red, Color.green, finalScore);
            
            // Draw a line from the LKP to the candidate node, colored by its score
            DebugShapeVisualizer.DrawLine(visualizerOwner, lkp, candidatePosition, scoreColor);
            DebugShapeVisualizer.DrawSphere(visualizerOwner, candidatePosition, 0.2f, scoreColor);
        }

        return finalScore;
    }
}