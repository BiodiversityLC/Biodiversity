using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Util;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class OccupancyMapSearch : SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter>
{
    private sealed class Cell
    {
        public GameObject NodeGameObject; // todo: remove this reference
        public Vector3 Position;
        public float Probability;
        public int[] Neighbours;
        public float[] NeighbourCost;
    }

    private readonly NavMeshPath _navMeshPath = new();

    // Tunable variables
    private readonly float _adjacencyRadius;
    private readonly float _diffusionRate;
    private readonly float _velocityBias;
    private readonly float _terminationMass;

    // How many milliseconds of graph-building work we permit per frame.
    private readonly float _graphBuildBudgetMs;

    private Cell[] _cells;
    private float[] _nextProb;

    private enum GraphState { None, Building, Ready }
    private GraphState _graphState = GraphState.None;

    private Coroutine _buildGraphCoroutine; // todo: call ctx.Blackboard.NetcodeController.StopCoroutine(_buildGraphCoroutine) when the enemy gameobject is destroyed

    private float _diffuseAccumulator;
    private const float DIFFUSE_INTERVAL = 0.25f;

    public OccupancyMapSearch(
        AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx,
        float adjacencyRadius = 30f,
        float diffusionRate = 0.4f,
        float velocityBias = 2f,
        float terminationMass = 0.05f,
        float graphBuildBudgetMs = 1f,
        bool drawDebugField = false,
        bool drawDebugEdges = false) : base(ctx)
    {
        _adjacencyRadius = adjacencyRadius;
        _diffusionRate = diffusionRate;
        _velocityBias = velocityBias;
        _terminationMass = terminationMass;
        _graphBuildBudgetMs = graphBuildBudgetMs;
        _drawDebugField = drawDebugField;
        _drawDebugEdges = drawDebugEdges;
    }

    public override void Start()
    {
        // Build the graph once and cache it
        if (_graphState == GraphState.None)
        {
            BiodiversityPlugin.LogVerbose("[OccupancyMapSearch] Start: graph not yet built, launching build coroutine.");
            _graphState = GraphState.Building;
            _buildGraphCoroutine = context.Blackboard.NetcodeController.StartCoroutine(BuildGraphCoroutine());
        }

        // If the graph is already build, then seed it
        if (_graphState == GraphState.Ready)
        {
            BiodiversityPlugin.LogVerbose("[OccupancyMapSearch] Start: graph ready, seeding distribution.");
            SeedDistribution();
        }
        else
        {
            BiodiversityPlugin.LogVerbose($"[OccupancyMapSearch] Start: graph state is {_graphState}; using LKP fallback until build completes.");
        }

        _diffuseAccumulator = 0f;
    }

    public override void Update()
    {
        if (_graphState != GraphState.Ready) return;
        if (_cells == null || _cells.Length == 0) return;

        ClearCellsInLos();

        _diffuseAccumulator += Time.deltaTime;
        while (_diffuseAccumulator >= DIFFUSE_INTERVAL)
        {
            DiffuseStep();
            _diffuseAccumulator -= DIFFUSE_INTERVAL;
        }
    }

    public override bool TryGetNextSearchPosition(out Vector3 nextPosition)
    {
        nextPosition = default;

        // The adjacency graph is build asynchronously and may not be ready. If so, we fall back to walking towards the player's last known position
        if (_graphState != GraphState.Ready)
        {
            nextPosition = context.Blackboard.LastKnownPlayerPosition;
            BiodiversityPlugin.LogVerbose($"[OccupancyMapSearch] NextPosition: graph not ready ({_graphState}); returning LKP fallback {nextPosition}.");
            return true;
        }

        if (_cells == null || _cells.Length == 0)
        {
            BiodiversityPlugin.LogVerbose("[OccupancyMapSearch] NextPosition: no cells; cannot search.");
            return false;
        }

        ClearCellsInLos();

        // Total remaining probability mass is the termination signal: once we've observed away enough
        // of the field that little mass is left anywhere, the player is effectively not in any
        // unsearched cell and continuing would be kinda useless.
        // todo: Make the molten state actually carry on this search, because he has no guard post to attend to
        float totalMass = 0f;
        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
            totalMass += _cells[cellIdx].Probability;

        if (totalMass < _terminationMass)
        {
            BiodiversityPlugin.LogVerbose($"[OccupancyMapSearch] NextPosition: remaining mass {totalMass:F3} < termination {_terminationMass}; search exhausted.");
            return false;
        }

        Vector3 origin = context.Adapter.Transform.position;

        const float MIN_SEARCH_DISTANCE = 1.5f;

        int bestIdx = -1;
        float bestScore = float.MinValue;

        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
        {
            Cell cell = _cells[cellIdx];
            if (cell.Probability <= 0f) continue;

            float travelCost = TravelCost(origin, cell.Position);
            if (travelCost < Mathf.Max(0, MIN_SEARCH_DISTANCE)) continue;

            float score = cell.Probability / (travelCost + 1f);
            if (score > bestScore)
            {
                bestScore = score;
                bestIdx = cellIdx;
            }
        }

        if (bestIdx < 0)
        {
            BiodiversityPlugin.LogVerbose($"[OccupancyMapSearch] NextPosition: {totalMass:F3} mass remains but no reachable cell; giving up.");
            return false;
        }

        nextPosition = _cells[bestIdx].Position;
        BiodiversityPlugin.LogVerbose(
            $"[OccupancyMapSearch] NextPosition: chose cell {bestIdx} at {nextPosition}, " +
            $"P={_cells[bestIdx].Probability:F3}, score={bestScore:F4}, totalMass={totalMass:F3}.");

        return true;
    }

    public override void Conclude()
    {
        BiodiversityPlugin.LogVerbose("[OccupancyMapSearch] Conclude: clearing field and debug visuals.");

        if (_drawDebugField)
            DebugShapeVisualizer.Clear(this);

        // Set every cell to zero probability mass
        if (_cells == null) return;
        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
            _cells[cellIdx].Probability = 0f;
    }

    private IEnumerator BuildGraphCoroutine()
    {
        GameObject[] nodes = context.Adapter.AssignedAINodes;
        int numNodes = nodes.Length;

        BiodiversityPlugin.LogVerbose($"[OccupancyMapSearch] BuildGraph: starting over {numNodes} nodes, adjacencyRadius={_adjacencyRadius}, budget={_graphBuildBudgetMs}ms/frame.");

        // Allocate every cell and cache its position
        _cells = new Cell[numNodes];
        for (int cellIdx = 0; cellIdx < numNodes; cellIdx++)
        {
            _cells[cellIdx] = new Cell
            {
                NodeGameObject = nodes[cellIdx],
                Position = nodes[cellIdx].transform.position,
                Probability = 0f
            };
        }

        _nextProb = new float[numNodes];

        // Euclidean pre-filter: a cheap squared-distance test discards far pairs before we pay
        // for the expensive NavMesh.CalculatePath call below
        float adjacencyRadiusSqr = _adjacencyRadius * _adjacencyRadius;
        int areaMask = context.Adapter.Agent.areaMask;

        // Temporary adjacency lists per node
        List<int> neighbourIndices = [];
        List<float> neighbourCosts = [];

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Aggregates for the completion log
        int totalEdges = 0;
        int isolatedCells = 0;
        int yieldCount = 0;

        for (int sourceIdx = 0; sourceIdx < numNodes; sourceIdx++)
        {
            neighbourIndices.Clear();
            neighbourCosts.Clear();
            Vector3 sourcePosition = _cells[sourceIdx].Position;

            for (int candidateIdx = 0; candidateIdx < numNodes; candidateIdx++)
            {
                if (sourceIdx == candidateIdx) continue;

                Vector3 candidatePosition = _cells[candidateIdx].Position;

                // Cheap reject: too far in a straight line means too far on the navmesh too
                if ((sourcePosition - candidatePosition).sqrMagnitude > adjacencyRadiusSqr) continue;

                // Expensive accept test: there must be a complete navmesh path between the two,
                // so cells on opposite sides of a wall are never linked even when physically close
                if (!NavMesh.CalculatePath(sourcePosition, candidatePosition, areaMask, _navMeshPath) ||
                    _navMeshPath.status != NavMeshPathStatus.PathComplete)
                    continue;

                float pathDistance = PathLength(_navMeshPath);
                if (pathDistance > _adjacencyRadius) continue;

                neighbourIndices.Add(candidateIdx);
                neighbourCosts.Add(pathDistance);

                // A single densely-connected node can blast the whole budget by itself, so we
                // also check the timer inside the inner loop, not just once per source cell
                if (stopwatch.Elapsed.TotalMilliseconds >= _graphBuildBudgetMs)
                {
                    yield return null;
                    yieldCount++;
                    stopwatch.Restart();
                }
            }

            _cells[sourceIdx].Neighbours = neighbourIndices.ToArray();
            _cells[sourceIdx].NeighbourCost = neighbourCosts.ToArray();

            // Check our graph building budget
            totalEdges += neighbourIndices.Count;
            if (neighbourIndices.Count == 0) isolatedCells++;

            if (stopwatch.Elapsed.TotalMilliseconds >= _graphBuildBudgetMs)
            {
                yield return null;
                yieldCount++;
                stopwatch.Restart();
            }
        }

        _graphState = GraphState.Ready;
        _buildGraphCoroutine = null;

        // isolatedCells diagnostic: a high count means _adjacencyRadius is too small and
        // the graph is fragmenting into islands that mass can't flow between.
        float avgDegree = numNodes > 0 ? (float)totalEdges / numNodes : 0f;
        BiodiversityPlugin.LogVerbose(
            $"[OccupancyMapSearch] BuildGraph: complete. {totalEdges} directed edges, " +
            $"avgDegree={avgDegree:F1}, isolatedCells={isolatedCells}, spread over {yieldCount} yields.");

        if (isolatedCells > 0)
            BiodiversityPlugin.Logger.LogWarning(
                $"[OccupancyMapSearch] BuildGraph: WARNING {isolatedCells} cell(s) have no neighbours; " +
                $"consider raising _adjacencyRadius (currently {_adjacencyRadius}).");

        SeedDistribution();
    }

    /// <summary>
    /// Resets the probability field and places the entire probability mass (1.0) on the single
    /// cell nearest the player's last known position. This is the initial spike that
    /// <see cref="DiffuseStep"/> subsequently spreads outward over time.
    /// </summary>
    /// <remarks>
    /// It cannot be ran if the graph has not been built yet (<see cref="_cells"/> is null); in that case it
    /// is re-invoked by <see cref="BuildGraphCoroutine"/> once construction completes.
    /// </remarks>
    private void SeedDistribution()
    {
        if (_cells == null)
        {
            BiodiversityPlugin.LogVerbose("[OccupancyMapSearch] SeedDistribution: cells is null (graph not built yet).");
            return;
        }

        // Wipe any leftover mass from a previous hunt before re-seeding
        int numCells  = _cells.Length;
        for (int cellIdx = 0; cellIdx < numCells; cellIdx++)
            _cells[cellIdx].Probability = 0f;

        Vector3 lastKnownPlayerPosition = context.Blackboard.LastKnownPlayerPosition;

        // Find the closest cell to the LKP by squared distance (no sqrt needed for a comparison).
        // All mass goes here; everywhere else starts at zero and only gains probability through
        // diffusion. This models "we know exactly where they were the instant we lost them."
        int seedIdx = -1;
        float nearestDistanceSqr = float.MaxValue;
        for (int cellIdx = 0; cellIdx < numCells; cellIdx++)
        {
            float distanceSqr = (_cells[cellIdx].Position - lastKnownPlayerPosition).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                seedIdx = cellIdx;
            }
        }

        if (seedIdx >= 0)
        {
            _cells[seedIdx].Probability = 1f;
            BiodiversityPlugin.LogVerbose(
                $"[OccupancyMapSearch] SeedDistribution: seeded cell {seedIdx} at {_cells[seedIdx].Position} " +
                $"({Mathf.Sqrt(nearestDistanceSqr):F1}m from LKP {lastKnownPlayerPosition}).");
        }
        else
        {
            BiodiversityPlugin.LogVerbose("[OccupancyMapSearch] SeedDistribution: no cell found to seed (empty graph?).");
        }

    }

    /// <summary>
    /// Advances the occupancy field by one diffusion step: each cell sheds a
    /// <see cref="_diffusionRate"/> fraction of its probability and distributes it among its
    /// navmesh neighbours, weighted by alignment with the player's last known velocity. The
    /// distribution therefore drifts along the player's likely escape route rather than expanding
    /// as a symmetric shell (like the <see cref="UtilityDrivenSearch"/>). If the player was stationary, mass spreads evenly.
    /// </summary>
    /// <remarks>
    /// Double-buffered through <see cref="_nextProb"/> so every cell diffuses from the same
    /// pre-step snapshot; computing in place would let mass cascade several hops in one step.
    /// Mass is conserved here, it only leaves the field via <see cref="ClearCellsInLos"/>.
    /// </remarks>
    private void DiffuseStep()
    {
        Vector3 lastKnownPlayerVelocity = context.Blackboard.LastKnownPlayerVelocity;

        // Treat a near-zero velocity as "no information about heading" and fall back to uniform spreading
        bool hasVelocity = lastKnownPlayerVelocity.sqrMagnitude > 0.1f;
        Vector3 velocityDirection = hasVelocity ? lastKnownPlayerVelocity.normalized : Vector3.zero;

        // Seed the next-state buffer with the current state. Cells that don't diffuse (zero mass
        // or no neighbours) then simply carry over unchanged
        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
            _nextProb[cellIdx] = _cells[cellIdx].Probability;

        int activeCells = 0; // Cells holding meaningful mass, for the summary
        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
        {
            Cell cell = _cells[cellIdx];
            float probability = cell.Probability;

            // Nothing to spread, or nowhere to spread it
            if (probability <= 0f || cell.Neighbours.Length == 0) continue;

            activeCells++;

            // The portion of this cell's mass that leaves this step; the rest stays put
            float spreadingMass = probability * _diffusionRate;
            _nextProb[cellIdx] -= spreadingMass;

            // First pass: total the per-neighbour weights so we can normalise the split. Weight is
            // 1 for a neutral/backward neighbour and rises toward (1 + _velocityBias) for one lying
            // directly along the player's heading. We clamp alignment at 0 so neighbours behind
            // the heading still receive the baseline share rather than a negative one
            float weightSum = 0f;
            for (int neighbourSlot = 0; neighbourSlot < cell.Neighbours.Length; neighbourSlot++)
            {
                weightSum += NeighbourWeight(
                    cell,
                    cell.Neighbours[neighbourSlot],
                    cell.NeighbourCost[neighbourSlot],
                    velocityDirection,
                    hasVelocity);
            }

            if (weightSum <= 0f) continue;

            // Second pass: hand each neighbour its normalised share of the shed mass. Recomputing
            // the weight here (rather than caching the first pass) keeps memory flat; neighbour
            // counts are tiny so the duplicated dot product is negligible
            for (int neighbourSlot = 0; neighbourSlot < cell.Neighbours.Length; neighbourSlot++)
            {
                float weight = NeighbourWeight(
                    cell,
                    cell.Neighbours[neighbourSlot],
                    cell.NeighbourCost[neighbourSlot],
                    velocityDirection,
                    hasVelocity);

                _nextProb[cell.Neighbours[neighbourSlot]] += spreadingMass * (weight / weightSum);
            }
        }

        // Commit the next state back into the cells
        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
            _cells[cellIdx].Probability = _nextProb[cellIdx];

        DrawDebugField();

        BiodiversityPlugin.LogVerbose(
            $"[OccupancyMapSearch] DiffuseStep: {activeCells} active cell(s), " +
            $"hasVelocity={hasVelocity}, rate={_diffusionRate}.");
    }

    /// <summary>
    /// Computes the diffusion weight for spreading probability mass from one cell to a neighbour.
    /// The weight combines two factors: alignment with the player's last known heading (neighbours
    /// in the direction the player was moving are favoured) and proximity (nearer neighbours are
    /// favoured, since the player is less likely to have reached a distant cell in the same time).
    /// </summary>
    /// <param name="from">The cell shedding mass.</param>
    /// <param name="neighbourIndex">Index into <see cref="_cells"/> of the receiving neighbour.</param>
    /// <param name="edgeCost">Navmesh travel distance between the two cells.</param>
    /// <param name="velocityDirection">Normalised last-known-velocity direction, or zero if unknown.</param>
    /// <param name="hasVelocity">False when the player was effectively stationary; disables the alignment term.</param>
    /// <returns>A strictly positive weight; callers normalise these across a cell's neighbours.</returns>
    private float NeighbourWeight(
        Cell from,
        int neighbourIndex,
        float edgeCost,
        Vector3 velocityDirection,
        bool hasVelocity)
    {
        // Directional term: 1 for a neutral/backward neighbour, rising toward (1 + _velocityBias)
        // for one lying directly along the heading. Alignment is clamped at 0 so cells behind the
        // heading keep the baseline share rather than dropping below it
        float directionalWeight = 1f;
        if (hasVelocity)
        {
            Vector3 directionToNeighbour = (_cells[neighbourIndex].Position - from.Position).normalized;
            float alignment = Vector3.Dot(directionToNeighbour, velocityDirection);
            directionalWeight = 1f + _velocityBias * Mathf.Max(0f, alignment);
        }

        // Proximity term: 1 / (1 + cost) falls off smoothly with distance and never hits zero, so a
        // far neighbour still receives some mass
        float proximityWeight = 1f / (1f + edgeCost);

        return directionalWeight * proximityWeight;
    }

    /// <summary>
    /// Zeroes the probability of every cell currently within the enemy's line of sight. This is
    /// the observation step: somewhere the enemy can presently see is somewhere the player
    /// demonstrably is not, so that mass is removed from the field. Driving total mass below
    /// <see cref="_terminationMass"/> is what eventually ends the search.
    /// </summary>
    private void ClearCellsInLos()
    {
        int clearedCount = 0;
        float clearedMass = 0f;

        for (int i = 0; i < _cells.Length; i++)
        {
            Cell cell = _cells[i];
            if (cell.Probability <= 0f) continue;

            if (LineOfSightUtil.HasLineOfSight(
                    cell.Position,
                    context.Adapter.EyeTransform,
                    context.Blackboard.ViewWidth,
                    context.Blackboard.ViewRange,
                    2f))
            {
                clearedMass += cell.Probability;
                cell.Probability = 0f;
                clearedCount++;
            }
        }

        if (clearedCount > 0)
            BiodiversityPlugin.LogVerbose(
                $"[OccupancyMapSearch] ClearVisibleCells: observed and cleared {clearedCount} cell(s), removed {clearedMass:F3} mass.");
    }

    /// <summary>
    /// Computes the navmesh traversal distance between two points.
    /// </summary>
    /// <param name="from">Start position.</param>
    /// <param name="to">Destination position.</param>
    /// <returns>
    /// The summed length of the navmesh path corners, or <c>-1</c> if no complete path exists
    /// (caller treats a negative result as unreachable).
    /// </returns>
    private float TravelCost(Vector3 from, Vector3 to)
    {
        if (!NavMesh.CalculatePath(from, to, context.Adapter.Agent.areaMask, _navMeshPath) ||
            _navMeshPath.status != NavMeshPathStatus.PathComplete)
            return -1f;

        return PathLength(_navMeshPath);
    }

    /// <summary>
    /// Sums the straight-line distances between consecutive corners of a navmesh path to give
    /// its total traversal length.
    /// </summary>
    /// <param name="path">A computed navmesh path.</param>
    /// <returns>The total length along the path's corners; <c>0</c> for a path with fewer than two corners.</returns>
    private static float PathLength(NavMeshPath path)
    {
        float distance = 0f;
        Vector3[] corners = path.corners;

        for (int i = 1; i < corners.Length; i++)
            distance += Vector3.Distance(corners[i - 1], corners[i]);

        return distance;
    }

    #region Debug
    private readonly bool _drawDebugField;
    private readonly bool _drawDebugEdges; // also draw adjacency edges (noisy on big graphs)
    private const float DEBUG_MIN_PROBABILITY = 0.001f; // don't draw effectively-empty cells
    private const float DEBUG_SPHERE_BASE_RADIUS = 0.15f;
    private const float DEBUG_SPHERE_PROB_SCALE = 0.6f; // extra radius at probability 1.0

    /// <summary>
    /// Renders the current occupancy field for debugging: one sphere per cell, sized and coloured
    /// by its probability (cold/small = unlikely, hot/large = likely), and optionally the adjacency
    /// edges between cells. Only works unless <see cref="_drawDebugField"/> is set.
    /// </summary>
    private void DrawDebugField()
    {
        if (!_drawDebugField || _cells == null) return;

        DebugShapeVisualizer.Clear(this);

        // Find the current peak so colour/size scale relative to the live maximum rather than a
        // fixed 1. After diffusion spreads the initial spike, no cell is near 1 and a fixed
        // scale would render the whole field uniformly dim
        float peakProbability = 0f;
        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
        {
            if (_cells[cellIdx].Probability > peakProbability)
                peakProbability = _cells[cellIdx].Probability;
        }

        if (peakProbability <= 0f) return;

        for (int cellIdx = 0; cellIdx < _cells.Length; cellIdx++)
        {
            Cell cell = _cells[cellIdx];
            if (cell.Probability < DEBUG_MIN_PROBABILITY) continue;

            // Normalise against the peak so the hottest cell is always fully saturated
            float normalized = cell.Probability / peakProbability;

            // Blue (cold, unlikely) -> red (hot, likely)
            Color colour = new(normalized, 0f, 1f - normalized, 0.6f);

            float radius = DEBUG_SPHERE_BASE_RADIUS + normalized * DEBUG_SPHERE_PROB_SCALE;
            DebugShapeVisualizer.DrawSphere(this, cell.Position, radius, colour);

            if (_drawDebugEdges && cell.Neighbours != null)
            {
                // Only draw edges outward from cells that hold meaningful mass, otherwise every
                // edge in the graph is drawn twice and the view turns to spaghetti
                for (int neighbourSlot = 0; neighbourSlot < cell.Neighbours.Length; neighbourSlot++)
                {
                    Vector3 neighbourPos = _cells[cell.Neighbours[neighbourSlot]].Position;
                    DebugShapeVisualizer.DrawLine(this, cell.Position, neighbourPos, new Color(0.3f, 0.3f, 0.3f, 0.25f));
                }
            }
        }
    }
    #endregion
}