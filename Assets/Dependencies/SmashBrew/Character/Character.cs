using HouraiTeahouse.SmashBrew.States;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HouraiTeahouse.SmashBrew.Characters {

    /// <summary> General character class for handling the physics and animations of individual characters </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(MovementState))]
    public class Character : NetworkBehaviour, IHitboxController, IRegistrar<ICharacterComponent> {

        public CharacterController Controller { get; private set; }
        public MovementState Movement { get; private set; }
        public StateController<CharacterState, CharacterStateContext> StateController { get; private set; }
        public CharacterStateContext Context { get; private set; }

        public CharacterControllerBuilder States {
            get { return _controller; }
        }

        Dictionary<int, Hitbox> _hitboxMap;
        Dictionary<int, CharacterState> _stateMap;
        List<Hitbox> _hurtboxes;
        List<ICharacterComponent> _components;

        [SerializeField]
        CharacterControllerBuilder _controller;


        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        void Awake() {
            gameObject.tag = Config.Tags.PlayerTag;
            gameObject.layer = Config.Tags.CharacterLayer;
            if (_controller == null)
                throw new InvalidOperationException("Cannot start a character without a State Controller!");
            StateController = _controller.BuildCharacterControllerImpl(
                new StateControllerBuilder<CharacterState, CharacterStateContext>());
            if (Debug.isDebugBuild)
                StateController.OnStateChange += (b, a) => Log.Debug("{0} changed states: {1} => {2}".With(name, b.Name, a.Name));
            Context = new CharacterStateContext();
            _hitboxMap = new Dictionary<int, Hitbox>();
            _hurtboxes = new List<Hitbox>();
            _components = new List<ICharacterComponent>();
            _stateMap = StateController.States.ToDictionary(s => s.AnimatorHash);
            Controller = this.SafeGetComponent<CharacterController>();
            Movement = this.SafeGetComponent<MovementState>();
            EstablishImmunityChanges();
        }

        void EstablishImmunityChanges() {
            var  typeMap = new Dictionary<ImmunityType, Hitbox.Type> {
                {ImmunityType.Normal, Hitbox.Type.Damageable},
                {ImmunityType.Intangible, Hitbox.Type.Intangible},
                {ImmunityType.Invincible, Hitbox.Type.Invincible}
            };
            StateController.OnStateChange += (b, a) => {
                if (_hurtboxes == null || _hurtboxes.Count < 0)
                    return;
                var hitboxType = Hitbox.Type.Damageable;
                typeMap.TryGetValue(a.Data.DamageType, out hitboxType);
                foreach (var hurtbox in _hurtboxes)
                    hurtbox.CurrentType = hitboxType;
            };
        }

        void IRegistrar<Hitbox>.Register(Hitbox hitbox) {
            int id = Argument.NotNull(hitbox).ID;
            if (_hitboxMap.ContainsKey(id))
                Log.Error("Hitboxes {0} and {1} on {2} have the same id. Ensure that they have different IDs.",
                    hitbox,
                    _hitboxMap[id],
                    gameObject.name);
            else {
                _hitboxMap.Add(id, hitbox);
                _hurtboxes.Add(hitbox);
            }
        }

        bool IRegistrar<Hitbox>.Unregister(Hitbox obj) {
            return _hitboxMap.Remove(Argument.NotNull(obj).ID) || _hurtboxes.Remove(obj);
        }

        void IRegistrar<ICharacterComponent>.Register(ICharacterComponent component) {
            if (_components.Contains(Argument.NotNull(component)))
                return;
            _components.Add(component);
        }

        bool IRegistrar<ICharacterComponent>.Unregister(ICharacterComponent component) {
            return _components.Remove(component);
        }

        /// <summary> Retrieves a hitbox given it's ID. </summary>
        /// <param name="id"> the ID to look for </param>
        /// <returns> the hitbox if found, null otherwise. </returns>
        public Hitbox GetHitbox(int id) {
            return _hitboxMap.GetOrDefault(id);
        }

        public void ResetAllHitboxes() {
            foreach (Hitbox hitbox in Hitboxes.IgnoreNulls()) {
                if (hitbox.ResetType())
                    Log.Info("{0} {1}", this, hitbox);
            }
        }

        #region Unity Callbacks

        void OnEnable() { _isActive = true; }

        void OnDisable() { _isActive = false; }

        public override void OnStartAuthority() {
            // Update server when the local client has changed.
            StateController.OnStateChange += (b, a) => CmdChangeState(a.AnimatorHash);
        }

        // public override void OnStartServer() {
        //     _isActive = true;
        //     // Update clients when a state has changed server-side.
        //     StateController.OnStateChange += (b, a) => RpcUpdateState(a.AnimatorHash);
        // }

        void LateUpdate() {
            if (!hasAuthority)
                return;
            foreach (var component in _components)
                component.UpdateStateContext(Context);
            StateController.UpdateState(Context);
        }
        #endregion

        #region Public Properties
        /// <summary> Gets an immutable collection of hitboxes that belong to </summary>
        public ICollection<Hitbox> Hitboxes {
            get { return _hitboxMap.Values; }
        }

        public void ResetCharacter() {
            StateController.ResetState();
            foreach (IResettable resetable in GetComponentsInChildren<IResettable>().IgnoreNulls())
                resetable.OnReset();
        }
        #endregion

#pragma warning disable 414
        [SerializeField, ReadOnly, SyncVar(hook = "ChangeActive")]
        bool _isActive;
#pragma warning restore 414

        // Network Callbacks

        void ChangeActive(bool active) {
            _isActive = active;
            gameObject.SetActive(active);
        }

        [Command]
        void CmdChangeState(int stateHash) {
            CharacterState state;
            if (!_stateMap.TryGetValue(stateHash, out state)) {
                Log.Error("Client attempted to set state to one with hash {0}, which has no matching server state.");
                return;
            }
            StateController.SetState(state);
        }

        [ClientRpc]
        void RpcUpdateState(int stateHash) {
            CharacterState state;
            if (!_stateMap.TryGetValue(stateHash, out state)) {
                Log.Error("Server attempted to set state to one with hash {0}, which has no matching client state.");
                return;
            }
            StateController.SetState(state);
        }

    }

}
