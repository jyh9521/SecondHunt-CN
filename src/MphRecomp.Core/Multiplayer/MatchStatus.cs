using System.Collections.Generic;
using System.Text;
using MphRead;
using MphRead.Entities;
using MphRead.Text;
using MphRecomp.Frontend;

namespace MphRecomp.Multiplayer
{
    public readonly record struct SeatStatus(int Seat, Hunter Hunter, string Nickname, bool Bot, int Team,
        int Points, int Kills, int Deaths, float Time, int Standing, int Health, bool Active);

    // Read-only view of MphRead's match state (GameState's per-seat arrays) for HUD text, results and tests.
    public static class MatchStatus
    {
        public static MatchState State => GameState.MatchState;
        public static GameMode Mode => GameState.Mode;
        public static float TimeLeft => GameState.MatchTime; // seconds; -1 = no limit / ended
        public static int PointGoal => GameState.PointGoal;

        public static IReadOnlyList<SeatStatus> Seats()
        {
            var list = new List<SeatStatus>();
            for (int i = 0; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity p = PlayerEntity.Players[i];
                if (!p.LoadFlags.TestFlag(LoadFlags.SlotActive))
                {
                    continue;
                }
                list.Add(new SeatStatus(i, p.Hunter, GameState.Nicknames[i], p.IsBot, p.TeamIndex,
                    GameState.Points[i], GameState.Kills[i], GameState.Deaths[i], GameState.Time[i],
                    GameState.Standings[i], p.Health, p.LoadFlags.TestFlag(LoadFlags.Active)));
            }
            return list;
        }

        // The winner's seat once the match is over (GameState.ResultSlots is ordered by team rank, then player rank).
        public static int WinnerSeat => GameState.ResultSlots[0];

        public static string Scoreboard()
        {
            var sb = new StringBuilder();
            sb.Append(Mode).Append(' ').Append(State);
            if (TimeLeft >= 0)
            {
                sb.Append($" {(int)TimeLeft / 60}:{(int)TimeLeft % 60:00}");
            }
            foreach (SeatStatus s in Seats())
            {
                sb.Append($"\n#{s.Standing + 1} {s.Nickname,-8} {s.Hunter,-7}");
                if (s.Team >= 0 && MatchSettings.IsTeamMode(Mode))
                {
                    sb.Append(s.Team == 0 ? " orange" : " green ");
                }
                sb.Append($" pts {s.Points} k {s.Kills} d {s.Deaths}");
                if (s.Time != 0)
                {
                    sb.Append($" t {s.Time:0.0}");
                }
                if (s.Bot)
                {
                    sb.Append(" (bot)");
                }
            }
            return sb.ToString();
        }

        public static string DisplayScoreboard()
        {
            if (!LocalText.IsLocalized) return Scoreboard();
            return FormatDisplay(Mode, State, TimeLeft, Seats());
        }

        internal static string FormatDisplay(GameMode mode, MatchState state, float timeLeft, IReadOnlyList<SeatStatus> seats)
        {
            var sb = new StringBuilder(LocalText.FormatId("match.score.header", "{0} {1}",
                DisplayNames.Mode(mode), DisplayNames.State(state)));
            if (timeLeft >= 0) sb.Append($" {(int)timeLeft / 60}:{(int)timeLeft % 60:00}");
            foreach (SeatStatus seat in seats)
            {
                string hunter = DisplayNames.Hunter(seat.Hunter);
                string name = LocalText.MatchNickname(seat.Nickname, seat.Hunter, seat.Bot,
                    GameState.GeneratedNicknames[seat.Seat] ? seat.Seat + 1 : 0);
                sb.Append('\n').Append(LocalText.FormatId("match.score.seat", "#{0} {1} {2}  pts {3}  k {4}  d {5}",
                    seat.Standing + 1, name, hunter, seat.Points, seat.Kills, seat.Deaths));
                if (seat.Team >= 0 && MatchSettings.IsTeamMode(mode))
                    sb.Append(seat.Team == 0 ? LocalText.Id("match.score.orange", "  orange") : LocalText.Id("match.score.green", "  green"));
                if (seat.Time != 0) sb.Append(LocalText.FormatId("match.score.time", "  t {0:0.0}", seat.Time));
                if (seat.Bot) sb.Append(LocalText.Id("match.score.bot", " (bot)"));
            }
            return sb.ToString();
        }
    }
}
