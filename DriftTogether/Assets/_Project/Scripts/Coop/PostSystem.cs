using System.Collections.Generic;

namespace DriftTogether.Coop
{
    public enum RaftPost
    {
        None,
        Rudder,
        OarLeft,
        OarRight
    }

    /// <summary>
    /// Raft post occupancy rules (UC-02/UC-07): one player per post, one post
    /// per player; taking a new post releases the old; going overboard or
    /// disconnecting releases everything. Pure logic, host-side only.
    /// </summary>
    public sealed class PostSystem
    {
        readonly Dictionary<RaftPost, ulong> _occupants = new Dictionary<RaftPost, ulong>();

        public bool TryOccupy(RaftPost post, ulong clientId)
        {
            if (post == RaftPost.None)
                return false;
            if (_occupants.TryGetValue(post, out ulong current) && current != clientId)
                return false;

            ReleaseAll(clientId);
            _occupants[post] = clientId;
            return true;
        }

        public bool Release(RaftPost post, ulong clientId)
        {
            if (_occupants.TryGetValue(post, out ulong current) && current == clientId)
            {
                _occupants.Remove(post);
                return true;
            }
            return false;
        }

        public void ReleaseAll(ulong clientId)
        {
            var toFree = new List<RaftPost>();
            foreach (var pair in _occupants)
                if (pair.Value == clientId)
                    toFree.Add(pair.Key);
            foreach (var post in toFree)
                _occupants.Remove(post);
        }

        public ulong? OccupantOf(RaftPost post) =>
            _occupants.TryGetValue(post, out ulong id) ? id : (ulong?)null;

        public RaftPost PostOf(ulong clientId)
        {
            foreach (var pair in _occupants)
                if (pair.Value == clientId)
                    return pair.Key;
            return RaftPost.None;
        }
    }
}
