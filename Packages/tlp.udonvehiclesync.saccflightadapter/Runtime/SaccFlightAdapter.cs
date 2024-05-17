using JetBrains.Annotations;
using SaccFlightAndVehicles;
using TLP.UdonUtils;
using TLP.UdonUtils.Common;
using TLP.UdonUtils.Events;
using TLP.UdonUtils.Extensions;
using TLP.UdonUtils.Physics;
using TLP.UdonUtils.Sources.Time;
using TLP.UdonUtils.Sync;
using TLP.UdonVehicleSync.TLP.UdonVehicleSync.Runtime.Prototype;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[DefaultExecutionOrder(ExecutionOrder)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SaccFlightAdapter : TlpBaseBehaviour
{
    protected override int ExecutionOrderReadOnly => ExecutionOrder;

    [PublicAPI]
    public new const int ExecutionOrder = TlpExecutionOrder.Min; // ensure it starts before the SaccEntity

    private Transform _target;

    #region Dependencies
    [Header("Dependencies")]
    public PredictingSync Receiver;

    public PositionSendController PositionSendController;
    public RigidbodyVelocityProvider RigidbodyVelocityProvider;
    public UdonEvent OnRespawnEvent;

    [Tooltip(
            "Typically this is a GameObject sitting in the scene origin at (0,0,0) with no rotation. Used to have an object sync in another objects local space (experimental). If not set this script will attempt to find a GameObject called 'TLP_SyncOrigin' as fallback.")]
    public Transform SyncOrigin;

    [Tooltip("If not set this script will look in the siblings of this GameObject for the SaccEntity")]
    public SaccEntity SaccEntity;
    #endregion

    #region SaccFlight
    public void SFEXT_O_RespawnButton() {
        #region TLP_DEBUG
#if TLP_DEBUG
        DebugLog(nameof(SFEXT_O_RespawnButton));
#endif
        #endregion

        if (SaccEntity.Occupied) return;

        if (!OwnershipTransfer.TransferOwnership(Receiver.gameObject, Networking.LocalPlayer, true)) {
            Warn($"Failed to take ownership of {nameof(Receiver)} hierarchy");
            return;
        }

        Respawn();
    }

    public void SFEXT_G_RespawnButton() {
        #region TLP_DEBUG
#if TLP_DEBUG
        DebugLog(nameof(SFEXT_G_RespawnButton));
#endif
        #endregion

        if (SaccEntity.Occupied) return;

        Respawn();
    }

    public void SFEXT_G_Explode() {
        #region TLP_DEBUG
#if TLP_DEBUG
        DebugLog(nameof(SFEXT_G_Explode));
#endif
        #endregion

        Respawn();
    }

    public void SFEXT_O_MoveToSpawn() {
        #region TLP_DEBUG
#if TLP_DEBUG
        DebugLog(nameof(SFEXT_O_MoveToSpawn));
#endif
        #endregion

        if (!OwnershipTransfer.TransferOwnership(gameObject, Networking.LocalPlayer, true)) {
            Warn($"Failed to take ownership of {nameof(Receiver)} hierarchy");
            return;
        }

        Respawn();
    }
    #endregion

    #region Hook Implementations
    protected override bool SetupAndValidate() {
        if (!base.SetupAndValidate()) {
            return false;
        }

        if (!Utilities.IsValid(OnRespawnEvent)) {
            Error($"{nameof(OnRespawnEvent)} is not set");
            return false;
        }

        if (OnRespawnEvent.ListenerMethod != "OnRespawn") {
            Error($"{nameof(OnRespawnEvent)}.{nameof(OnRespawnEvent.ListenerMethod)} is not set to 'OnRespawn'");
            return false;
        }

        if (!OnRespawnEvent.AddListenerVerified(this, nameof(OnRespawn))) {
            return false;
        }

        if (!Utilities.IsValid(SaccEntity) && !TryFindSaccEntityOnSiblings()) {
            Error($"{nameof(SaccEntity)} is not set and not found on any sibling of {transform.GetPathInScene()}");
            return false;
        }


        if (!Utilities.IsValid(Receiver)) {
            Error($"{nameof(Receiver)} is not set");
            return false;
        }

        if (!Utilities.IsValid(SyncOrigin)) {
            var syncOrigin = GameObject.Find("TLP_SyncOrigin");
            if (syncOrigin) {
                SyncOrigin = syncOrigin.transform;
            } else {
                Error($"{nameof(SyncOrigin)} is not set and fallback 'TLP_SyncOrigin' not found in the scene");
                return false;
            }
        }

        if (!Utilities.IsValid(PositionSendController)) {
            Error($"{nameof(PositionSendController)} is not set");
            return false;
        }

        if (!Utilities.IsValid(RigidbodyVelocityProvider)) {
            Error($"{nameof(RigidbodyVelocityProvider)} is not set");
            return false;
        }

        SetupSaccExtensions();

        if (!SetupSaccRigidBody()) {
            ErrorAndDisableGameObject(
                    $"Failed to setup a {nameof(Rigidbody)} on {SaccEntity.transform.GetPathInScene()}");
            return false;
        }

        Receiver.NetworkTime = TlpNetworkTime.GetInstance();
        Receiver.Target = SaccEntity.transform;
        PositionSendController.NetworkTime = TlpNetworkTime.GetInstance();
        RigidbodyVelocityProvider.RelativeTo = SyncOrigin;

        return true;
    }

    public override void OnEvent(string eventName) {
        switch (eventName) {
            case nameof(OnRespawn):
                OnRespawn();
                break;
            default:
                base.OnEvent(eventName);
                break;
        }
    }
    #endregion

    #region Internal
    private void SetupSaccExtensions() {
        SaccEntity.ExtensionUdonBehaviours =
                SaccEntity.ExtensionUdonBehaviours.ResizeOrCreate(SaccEntity.ExtensionUdonBehaviours.Length + 1);
        SaccEntity.ExtensionUdonBehaviours[SaccEntity.ExtensionUdonBehaviours.Length - 1] = this;

        bool receiverAdded = false;
        for (int i = 0; i < SaccEntity.ExtensionUdonBehaviours.Length; i++) {
            var extensionUdonBehaviour = SaccEntity.ExtensionUdonBehaviours[i];
            if (!extensionUdonBehaviour) {
                continue;
            }

            if (extensionUdonBehaviour.GetUdonTypeID() == GetUdonTypeID<SAV_SyncScript>()) {
                SaccEntity.ExtensionUdonBehaviours[i] = Receiver;
                receiverAdded = true;
                break;
            }
        }

        if (!receiverAdded) {
            SaccEntity.ExtensionUdonBehaviours =
                    SaccEntity.ExtensionUdonBehaviours.ResizeOrCreate(SaccEntity.ExtensionUdonBehaviours.Length + 1);
            SaccEntity.ExtensionUdonBehaviours[SaccEntity.ExtensionUdonBehaviours.Length - 1] = Receiver;
        }
    }

    private bool SetupSaccRigidBody() {
        var foundRb = SaccEntity.gameObject.GetComponent<Rigidbody>();

        if (!RigidbodyVelocityProvider.ToTrack) {
            RigidbodyVelocityProvider.ToTrack = foundRb;
            if (!RigidbodyVelocityProvider.ToTrack) {
                return false;
            }
        } else if (foundRb) {
            if (!ReferenceEquals(RigidbodyVelocityProvider.ToTrack, foundRb)) {
                Warn(
                        $"A custom RigidBody '{RigidbodyVelocityProvider.ToTrack.GetComponentPathInScene()}' is being used");
            }
        }

        RigidbodyVelocityProvider.ToTrack.interpolation = RigidbodyInterpolation.Interpolate;
        RigidbodyVelocityProvider.ToTrack.drag = 0;
        RigidbodyVelocityProvider.ToTrack.angularDrag = 0;
        RigidbodyVelocityProvider.ToTrack.isKinematic = false;
        RigidbodyVelocityProvider.ToTrack.constraints = RigidbodyConstraints.None;
        return true;
    }

    private bool TryFindSaccEntityOnSiblings() {
        var parent = transform.parent;
        if (!transform.parent) {
            Error("Expected to be a child of a Saccflight vehicle prefab");
            return false;
        }

        SaccEntity = parent.GetComponentInChildren<SaccEntity>();
        return Utilities.IsValid(SaccEntity);
    }

    private void Respawn() {
        if (!Utilities.IsValid(Receiver)) {
            ErrorAndDisableGameObject($"{nameof(Receiver)} is not set");
            return;
        }

        Receiver.Respawn(false);
        if (!Receiver.TeleportTo(
                    SaccEntity.transform.parent.TransformPoint(SaccEntity.Spawnposition + Vector3.up * 0.05f),
                    SaccEntity.transform.parent.rotation * SaccEntity.Spawnrotation)) {
            Warn($"Failed respawn {nameof(Receiver)}");
        }
        SaccEntity.EntityRespawn();
    }

    /// <summary>
    /// Callback from PredictingSync when it e.g. falls below the min height
    /// </summary>
    private void OnRespawn() {
        #region TLP_DEBUG
#if TLP_DEBUG
        DebugLog(nameof(OnRespawn));
#endif
        #endregion

        SaccEntity._dead = true;
    }
    #endregion
}