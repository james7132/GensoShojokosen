﻿#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HouraiTeahouse.FantasyCrescendo.Characters {

public class StateMachineMetadata : ScriptableObject {

  public Vector2 WindowOffset = Vector2.zero;
  public Vector2 WindowZoomPivot = Vector2.zero;
  public float WindowZoom = 1.0f;

  Dictionary<int, StateNode> _stateDictionary;
  [SerializeField] List<StateNode> _stateNodes;
  [SerializeField] List<TransitionNode> _transitionNodes;

  public StateMachineAsset _stateMachine;

  public ReadOnlyDictionary<int, StateNode> StateDictionary => new ReadOnlyDictionary<int, StateNode>(_stateDictionary);
  public ReadOnlyCollection<StateNode> StateNodes => new ReadOnlyCollection<StateNode>(_stateNodes);
  public ReadOnlyCollection<TransitionNode> TransitionNodes => new ReadOnlyCollection<TransitionNode>(_transitionNodes);

  /// <summary>
  /// This function is called when the object becomes enabled and active.
  /// </summary>
  void OnEnable() => Initialize();

  void Initialize() {
    _stateMachine = _stateMachine ?? StateMachineAsset.GetStateMachineAsset();
    _stateNodes = _stateNodes ?? new List<StateNode>();
    _transitionNodes = _transitionNodes ?? new List<TransitionNode>();

    if (_stateDictionary == null) {
      _stateDictionary = new Dictionary<int, StateNode>(StateNodes.Count);
      foreach (var node in StateNodes) _stateDictionary.Add(node.Id, node);
    }
  }

  /// <summary>
  /// Used to save the editor window's state between sessions.
  /// </summary>
  [Serializable]
  public abstract class Node<T> where T : ScriptableObject {
    public int Id => Asset.GetInstanceID();
    public T Asset;
#if UNITY_EDITOR
    public bool IsSelected => Selection.objects.Contains(Asset);
#endif

    public abstract bool Contains(Vector2 position);
  }

  [Serializable]
  public class StateNode : Node<BaseStateAsset> {

    public Vector2 Center;
    [NonSerialized] public Vector2 PreviousCenter;
    public Rect Window {
      get {
        var nodeSize  = new Vector2(120, 50);
        return new Rect(Center - nodeSize / 2, nodeSize);
      }
    }
    public bool HasMoved => Center != PreviousCenter;

    public StateNode(BaseStateAsset asset) {
      Asset = asset;
    }

    public override bool Contains(Vector2 position) => Window.Contains(position);

    public string GetRichText() => Asset.name;
  }

  [Serializable]
  public class TransitionNode: Node<StateTransitionAsset> {
    public int SourceId => Asset.SourceState.GetInstanceID();
    public int DestinationId => Asset.DestinationState.GetInstanceID();

    [NonSerialized] public Vector2 CornerSource;
    [NonSerialized] public Vector2 CornerSourceAway;
    [NonSerialized] public Vector2 CornerDestination;
    [NonSerialized] public Vector2 CornerDestinationAway;

    [NonSerialized] public Vector2 Center;
    [NonSerialized] public Vector2 CenterSource;
    [NonSerialized] public Vector2 CenterDestination;
    
    [NonSerialized] public Vector2 ArrowLeftEnd;
    [NonSerialized] public Vector2 ArrowRightEnd;

    [NonSerialized] public float Area;

    public const float TransitionSize = 15f;
    public const float ArrowSize = 4f;

    public TransitionNode(StateTransitionAsset asset) {
      Asset = asset;
    }

    public bool Involves(int id) => SourceId == id || DestinationId == id; 

    public void UpdateVectors(StateNode src, StateNode dst, bool force = false){
      // Prevent updating every gui call if the window didn't move
      if (!force && (src.HasMoved || dst.HasMoved)) return;

      var Direction = (dst.Center - src.Center).normalized;
      var CornerOffet = GetCornerOffset(Direction);
      CornerSource = src.Center;
      CornerSourceAway = CornerSource + CornerOffet;
      CornerDestination = dst.Center;
      CornerDestinationAway = CornerDestination + CornerOffet;

      CenterSource = GetMiddle(CornerSource, CornerSourceAway);
      CenterDestination = GetMiddle(CornerDestination, CornerDestinationAway);
      Center = GetMiddle(CenterSource, CenterDestination);

      ArrowLeftEnd = GetArrowEnd(Direction, Center, Vector3.forward);
      ArrowRightEnd = GetArrowEnd(Direction, Center, Vector3.back);

      Area = GetAreaOfRectangle(CornerSource, CornerSourceAway, CornerDestinationAway);
    }

    private Vector2 GetMiddle(Vector2 a, Vector2 b) => 0.5f * (a + b);
    private Vector2 GetCornerOffset(Vector2 Direction) 
      => (Vector2)Vector3.Cross(Direction, Vector3.forward) * TransitionSize;
    private Vector2 GetArrowEnd(Vector2 Direction, Vector2 LineCenter, Vector3 CrossVector) 
      => LineCenter - (TransitionSize * Direction) + (Vector2)Vector3.Cross(Direction, CrossVector) * ArrowSize;

    public override bool Contains(Vector2 e){
      return Mathf.Approximately(Area, GetAreaOfTriangle(CornerSource, CornerSourceAway, e)
                                    + GetAreaOfTriangle(CornerSourceAway, CornerDestinationAway, e)
                                    + GetAreaOfTriangle(CornerDestinationAway, CornerDestination, e)
                                    + GetAreaOfTriangle(CornerDestination, CornerSource, e));
    }

    private float GetAreaOfTriangle(Vector2 a, Vector2 b, Vector2 c)
      => 0.5f * Mathf.Abs((a.x) * (b.y - c.y) + (b.x) * (c.y - a.y) + (c.x) * (a.y - b.y));

    private float GetAreaOfRectangle(Vector2 a, Vector2 b, Vector2 c)
        => Mathf.Sqrt(Vector2.SqrMagnitude(a - b) * Vector2.SqrMagnitude(b - c));
  }

  public StateNode FindState(StateAsset asset) => _stateNodes.FirstOrDefault(s => s.Asset == asset);
  public TransitionNode FindTransition(StateTransitionAsset asset) => _transitionNodes.FirstOrDefault(s => s.Asset == asset);

  /// <summary>
  /// Creates state editor node alongside a StateMachineAsset
  /// </summary>
  public StateNode AddStateNode<T>() where T : BaseStateAsset {
    var asset = _stateMachine.CreateState<T>("State");
    var state = new StateNode(asset);

    _stateNodes.Add(state);
    _stateDictionary.Add(state.Id, state);
    return state;
  }

  /// <summary>
  /// Creates transition editor node alongside a StateTransitionAsset
  /// </summary>
  public TransitionNode AddTransitionNode(StateNode src, StateNode dst) {
    Argument.NotNull(src);
    Argument.NotNull(dst);
    if (src == dst) {
      throw new InvalidOperationException("Cannot create a transition from a state to itself");
    }
    if (TransitionNodeExists(src, dst))  {
      throw new InvalidOperationException("Cannot create a transition that already exists");
    }
    var asset = src.Asset.CreateTransition(dst.Asset);
    var node = new TransitionNode(asset);
    node.UpdateVectors(src, dst, true);

    _transitionNodes.Add(node);
    return node;
  }

  /// <summary>
  /// Removes state editor node from metadata and state machine
  /// </summary>
  /// <param name="node"></param>
  public bool RemoveStateNode(StateNode node) {
    if (node == null) return false;
    if (_stateDictionary.Remove(node.Id)) {
      _stateMachine.RemoveState(node.Asset);
      _stateNodes.RemoveAll(n => n.Asset == null); 
      _transitionNodes.RemoveAll(t => t.Asset.SourceState == null || t.Asset.DestinationState == null);
      return true;
    }
    return false;
  }

  /// <summary>
  /// Removes transition editor node from metadata and state machine
  /// </summary>
  /// <param name="node"></param>
  /// <returns>true if the node was removed, false otherwise.</returns>
  public bool RemoveTransitionNode(TransitionNode node){
    if (node == null) return false;
    if (_transitionNodes.Remove(node)) {
      node.Asset.Destroy();
      return true;
    }
    return false;
  }

  /// <summary>
  /// Updates transition's vectors to drawing lines and detecting selection.
  /// </summary>
  public void UpdateTransitionNodes(){
    _transitionNodes.RemoveAll(node => node.Asset == null);
    _stateNodes.RemoveAll(node => node.Asset == null);
    foreach (var node in _transitionNodes) node.UpdateVectors(_stateDictionary[node.SourceId], _stateDictionary[node.DestinationId]);
    foreach (var node in _stateNodes) node.PreviousCenter = node.Center;
  }

  /// <summary>
  /// Checks if a transition exists between two nodes.
  /// </summary>
  /// <param name="src">the source node</param>
  /// <param name="dst">the target node</param>
  /// <returns>true if such a transition exists, false otherwise</returns>
  public bool TransitionNodeExists(StateNode src, StateNode dst) 
    => TransitionNodes.Any(t => t.SourceId == src.Id && t.DestinationId == dst.Id);

  public static StateMachineMetadata Create() {
    var meta = ScriptableObject.CreateInstance<StateMachineMetadata>();
    meta.name = "Metadata";
    meta.Initialize();
    return meta;
  }

  }
}

#endif