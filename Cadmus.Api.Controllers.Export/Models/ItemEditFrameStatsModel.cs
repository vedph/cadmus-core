using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cadmus.Api.Controllers.Export.Models;

/// <summary>
/// Parameters for item edit frame statistics.
/// </summary>
public class ItemEditFrameStatsModel
{
    /// <summary>
    /// The start date of the frame.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>
    /// The end date of the frame.
    /// </summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// The interval of the frame, e.g. "1h" for one hour. Allowed formats
    /// are a number followed by a letter: <c>m</c>inute, <c>h</c>our,
    /// <c>d</c>ay, <c>w</c>eek (=7 days), <c>M</c>onth (=30 days).
    /// </summary>
    [RegularExpression(@"^\d+[mhdwM]$",
        ErrorMessage = "Interval must be a number followed " +
        "by 'm'inutes, 'h'ours, 'd'ays, 'w'eeks, 'M'onth")]
    public string Interval { get; set; } = "1d";

    /// <summary>
    /// The maximum number of frames to return. The number of frames is defined
    /// by the period determined by the <see cref="Start"/> and <see cref="End"/>
    /// divided by the <see cref="Interval"/>.
    /// </summary>
    public int? FrameLimit { get; set; }

    /// <summary>
    /// Gets the time frames defined by the parameters of this model.
    /// </summary>
    /// <returns>Time frames.</returns>
    public IEnumerable<Tuple<DateTime, DateTime>> GetFrames()
    {
        string intervalText = Interval?.Trim() ?? "7d";
        char type = intervalText[^1];

        DateTime start;
        DateTime end;
        if (End.HasValue)
        {
            end = End.Value;
        }
        else if (type == 'd')
        {
            // for day intervals when End is null, set time to 23:59:59
            DateTime now = DateTime.UtcNow;
            end = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59,
                DateTimeKind.Utc);
        }
        else
        {
            end = DateTime.UtcNow;
        }

        TimeSpan interval;
        switch (type)
        {
            case 'm':
                interval = TimeSpan.FromMinutes(int.Parse(intervalText[..^1]));
                start = Start ?? end - TimeSpan.FromMinutes(10);
                break;
            case 'h':
                interval = TimeSpan.FromHours(int.Parse(intervalText[..^1]));
                start = Start ?? end - TimeSpan.FromHours(10);
                break;
            case 'd':
                interval = TimeSpan.FromDays(int.Parse(intervalText[..^1]));
                start = Start ?? new DateTime(
                    (end - TimeSpan.FromDays(10)).Date.Ticks, DateTimeKind.Utc);
                break;
            case 'w':
                interval = TimeSpan.FromDays(int.Parse(intervalText[..^1]) * 7);
                start = Start ?? new DateTime(
                    (end - TimeSpan.FromDays(70)).Date.Ticks, DateTimeKind.Utc);
                break;
            case 'M':
                interval = TimeSpan.FromDays(int.Parse(intervalText[..^1]) * 30);
                start = Start ?? new DateTime(
                    (end - TimeSpan.FromDays(300)).Date.Ticks, DateTimeKind.Utc);
                break;
            default:
                throw new ArgumentException("Invalid interval format");
        }

        DateTime current = start;
        int count = 0;

        while (current < end)
        {
            if (FrameLimit.HasValue && FrameLimit.Value > 0 &&
                ++count > FrameLimit.Value)
            {
                break;
            }

            DateTime next = current + interval;
            if (next >= end)
            {
                // if next would go past or equal the end,
                // make this the final interval that ends exactly at end
                yield return Tuple.Create(current, end);
                break;
            }

            yield return Tuple.Create(current, next);
            current = next;
        }
    }
}
