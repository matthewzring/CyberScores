using System;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public interface ICompetitionRoundLogicService
    {
        CompetitionRound InferRound(DateTimeOffset date);
    }

    public class CyberPatriotTenCompetitionRoundLogicService : ICompetitionRoundLogicService
    {
        public CompetitionRound InferRound(DateTimeOffset date)
        {

            // approximation of eastern time
            // the precision on this estimation is limited anyway
            DateTimeOffset easternDate = date.ToOffset(TimeSpan.FromHours(-5));

            // CP-X only
            if (!((easternDate.Year == 2017 && easternDate.Month > 6) || (easternDate.Year == 2018 && easternDate.Month < 6)))
            {
                // cannot estimate for non-CPX
                return 0;
            }

            int day = easternDate.Day;
            // 1-12
            switch (easternDate.Month)
            {
                case 11:
                    // November, round 1
                    return day == 3 || day == 4 || day == 5 || day == 11 ? CompetitionRound.Round1 : 0;
                case 12:
                    // December, round 2
                    return day == 8 || day == 9 || day == 10 || day == 16 ? CompetitionRound.Round2 : 0;
                case 1:
                    // January, states round
                    return day == 19 || day == 20 || day == 21 || day == 27 ? CompetitionRound.Round3 : 0;
                case 2:
                    // February, semifinals
                    return day == 9 || day == 10 || day == 11 || day == 17 ? CompetitionRound.Semifinals : 0;
            }

            // no round predicted on the given date
            return 0;
        }
    }
}