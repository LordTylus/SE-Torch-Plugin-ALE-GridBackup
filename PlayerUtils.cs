using Sandbox.Game.World;

namespace ALE_GridBackup
{
    class PlayerUtils
    {
        public static MyIdentity GetIdentityByNameOrId(string playerNameOrSteamId) {

            foreach (var identity in MySession.Static.Players.GetAllIdentities()) {

                if (identity.DisplayName == playerNameOrSteamId)
                    return identity;

                if(ulong.TryParse(playerNameOrSteamId, out ulong steamId)) {

                    ulong id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                    if(id == steamId)
                        return identity;
                }
            }

            return null;
        }

        public static MyIdentity GetIdentityByName(string playerName) {

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
                if (identity.DisplayName == playerName)
                    return identity;

            return null;
        }

        public static MyIdentity GetIdentityById(long playerId) {

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
                if (identity.IdentityId == playerId)
                    return identity;

            return null;
        }

        public static string GetPlayerNameById(long playerId) {

            MyIdentity identity = GetIdentityById(playerId);

            if (identity != null)
                return identity.DisplayName;

            return "Nobody";
        }
    }
}
