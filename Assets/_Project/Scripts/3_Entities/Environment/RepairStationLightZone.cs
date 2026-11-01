using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastSignal.Core.Bootstrap;
using TheLastSignal.Systems.Lighting;

namespace TheLastSignal.Entities.Environment
{
    /// <summary>
    /// A repair-station room's light zone: off by default, switched on server-side once a repair is
    /// confirmed. Mirrors DoorController's shape exactly (its own per-entity NetworkVariable, Server
    /// write / Everyone read, offline-fallback when there is no NetworkManager) since "12 doors can't
    /// share one singleton" applies identically here — see DoorController.cs and
    /// ARCHITECTURE_RULES.md Rule 1/2.
    ///
    /// Epic 8's Task Ticket System doesn't exist yet (TaskLocation.cs is an explicit placeholder with no
    /// real, server-validated repair-confirmation signal) — so nothing calls <see cref="ServerSetLit"/>
    /// today. This component is the ready-made hook Epic 8 wires a real confirmation into, rather than
    /// a half-wired call through TaskLocation's non-networked placeholder (see the task's own report for
    /// why that call site was deliberately left alone).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class RepairStationLightZone : NetworkBehaviour
    {
        [SerializeField] private GameplayLightSource lightSource;
        [SerializeField] private Light2D visualLight;
        [SerializeField] private SpriteRenderer visualSprite;

        private readonly NetworkVariable<bool> lit = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool offlineLit;

        public bool IsLit => IsSpawned ? lit.Value : offlineLit;

        private void Awake()
        {
            if (lightSource == null)
            {
                lightSource = GetComponentInChildren<GameplayLightSource>();
            }

            if (visualLight == null)
            {
                visualLight = GetComponentInChildren<Light2D>();
            }

            if (visualSprite == null)
            {
                visualSprite = GetComponentInChildren<SpriteRenderer>();
            }

            if (GameBootstrap.CurrentMode == LaunchMode.DedicatedServer)
            {
                Light2DStripper.StripVisuals(gameObject);
            }

            ApplyVisual(offlineLit);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            lit.OnValueChanged += HandleLitChanged;

            if (IsServer)
            {
                lit.Value = false;
            }

            ApplyVisual(IsLit);
        }

        public override void OnNetworkDespawn()
        {
            lit.OnValueChanged -= HandleLitChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only entry point. Call once a repair is actually confirmed server-side.</summary>
        public void ServerSetLit(bool value)
        {
            if (IsSpawned)
            {
                if (!IsServer)
                {
                    Debug.LogWarning($"[RepairStationLightZone] ServerSetLit called on a non-server peer for '{name}'; ignored.");
                    return;
                }

                lit.Value = value;
            }
            else
            {
                offlineLit = value;
                ApplyVisual(value);
            }
        }

        private void HandleLitChanged(bool previous, bool current)
        {
            ApplyVisual(current);
        }

        private void ApplyVisual(bool isLit)
        {
            // lightSource is server-truth only (LightingController never runs on a client) — write it
            // only when this peer is actually authoritative (the server, or offline single-player).
            if (lightSource != null && (!IsSpawned || IsServer))
            {
                lightSource.ServerSetEnabled(isLit);
            }

            if (visualLight != null)
            {
                visualLight.enabled = isLit;
            }

            if (visualSprite != null)
            {
                visualSprite.color = isLit ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
            }
        }
    }
}
