using System.Collections.Generic;

namespace DevTools.TeamActivity
{
    /// <summary>
    /// Replace this implementation to connect Supabase, Firebase, or another backend.
    /// The rest of Team Activity only depends on this contract.
    /// </summary>
    internal interface ITeamActivityTransport
    {
        void Publish(TeamMemberPresence presence);
        IReadOnlyList<TeamMemberPresence> ReadAll();
        void Remove(string clientId);
    }
}
