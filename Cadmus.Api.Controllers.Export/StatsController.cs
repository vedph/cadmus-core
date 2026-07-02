using Cadmus.Api.Controllers.Export.Models;
using Cadmus.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using System;
using System.Collections.Generic;

namespace Cadmus.Api.Controllers.Export;

[Authorize]
[ApiController]
public sealed class StatsController : ControllerBase
{
    private readonly IConfiguration _config;

    public StatsController(IConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets item edit frame statistics.
    /// </summary>
    /// <param name="model">Parameters for getting frames.</param>
    /// <returns>List of frame statistics.</returns>
    [HttpGet("api/stats/edit-frames")]
    [ProducesResponseType(200)]
    public IList<ItemEditFrameStats> GetEditFrameStats(
        [FromQuery] ItemEditFrameStatsModel model)
    {
        // create framer
        CadmusMongoDataFramer framer = new(
            new CadmusMongoDataFramerOptions()
            {
                ConnectionString = _config.GetConnectionString("Default")!,
                DatabaseName = _config.GetValue<string>("DatabaseNames:Data")!,
                IsIncremental = true,
                NoParts = true,
            });

        List<ItemEditFrameStats> frameStats = [];
        foreach (Tuple<DateTime, DateTime> frame in model.GetFrames())
        {
            ItemEditFrameStats stats = new()
            {
                Start = frame.Item1,
                End = frame.Item2,
            };

            // get stats for this frame
            foreach (BsonDocument item in framer.GetItems(
                new CadmusDumpFilter
                {
                    MinModified = frame.Item1,
                    MaxModified = frame.Item2,
                }))
            {
                switch (item["_status"].AsInt32)
                {
                    case 0:
                        stats.CreatedCount++;
                        break;
                    case 1:
                        stats.UpdatedCount++;
                        break;
                    case 2:
                        stats.DeletedCount++;
                        break;
                }
            }

            frameStats.Add(stats);
        }

        return frameStats;
    }
}
