using MphRead;
using MphRead.Text;
using MphRecomp.Multiplayer;

namespace MphRecomp.Frontend;

// Presentation only. Room names, hunter IDs, settings values, replay headers
// and wire enums remain in their original namespaces.
public static class DisplayNames
{
    public static string Room(RoomMetadata? room, string fallback = "") => room == null
        ? fallback : LocalText.Id("room." + room.Name.ToLowerInvariant(), room.InGameName ?? room.Name);

    public static string Arena(string key) => Room(MatchArenas.Find(key), key);

    public static string Mode(GameMode mode, string? fallback = null)
        => LocalText.Id("match.mode." + mode, fallback ?? mode.ToString());

    public static string Hunter(Hunter hunter, string? fallback = null)
        => LocalText.Id("hunter." + hunter, fallback ?? hunter.ToString());

    public static string Weapon(BeamType weapon) => LocalText.Id("weapon." + weapon, weapon.ToString());

    public static string State(MatchState state) => LocalText.Id("match.state." + state, state.ToString());

    public static string MapPiece(CorridorMap.Piece piece) => LocalText.Id("map.piece." + piece.Name, piece.Name);

    public static string MapPart(AreaMapModel.Part part) => part.IsConnector
        ? LocalText.FormatId("map.connector", "{0}", part.Name, part.ConnectorNumber)
        : Room(Metadata.GetRoomById(part.RoomId, noThrow: true), part.Name);
}
