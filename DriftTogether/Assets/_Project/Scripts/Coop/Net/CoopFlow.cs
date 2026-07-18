using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// Network event fan-out for the co-op run. Lives on the raft prefab so
    /// every peer has it; forwards host events to local presentation via
    /// CoopBootstrap.
    /// </summary>
    public sealed class CoopFlow : NetworkBehaviour
    {
        [Rpc(SendTo.Everyone)]
        public void RaftHitClientRpc(float impulse)
        {
            CoopBootstrap.Active?.ClientRaftHit(impulse);
        }

        [Rpc(SendTo.Everyone)]
        public void RaftBumpClientRpc()
        {
            CoopBootstrap.Active?.ClientRaftBump();
        }

        [Rpc(SendTo.Everyone)]
        public void SplashClientRpc()
        {
            CoopBootstrap.Active?.ClientSplash();
        }

        [Rpc(SendTo.Everyone)]
        public void CampfireRestClientRpc()
        {
            CoopBootstrap.Active?.ClientCampfireRest();
        }

        [Rpc(SendTo.Everyone)]
        public void TimLineClientRpc(FixedString128Bytes line)
        {
            CoopBootstrap.Active?.ClientTimLine(line.ToString());
        }

        [Rpc(SendTo.Everyone)]
        public void FinishClientRpc(CoopReportPayload payload)
        {
            CoopBootstrap.Active?.ClientFinish(payload);
        }

        [Rpc(SendTo.Everyone)]
        public void CapsizedClientRpc()
        {
            CoopBootstrap.Active?.ClientCapsized();
        }

        [Rpc(SendTo.Everyone)]
        public void RightedClientRpc()
        {
            CoopBootstrap.Active?.ClientRighted();
        }

        [Rpc(SendTo.Everyone)]
        public void WaterfallClientRpc()
        {
            CoopBootstrap.Active?.ClientWaterfall();
        }

        [Rpc(SendTo.Everyone)]
        public void RevealZoneClientRpc(int zoneIndex)
        {
            CoopBootstrap.Active?.RevealZoneBuoys(zoneIndex);
        }
    }
}
