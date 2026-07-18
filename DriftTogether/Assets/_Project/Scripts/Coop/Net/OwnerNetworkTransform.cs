using Unity.Netcode.Components;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// Owner-authoritative transform sync for player avatars: casual friend
    /// co-op, the owning client is trusted with its own position.
    /// </summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
