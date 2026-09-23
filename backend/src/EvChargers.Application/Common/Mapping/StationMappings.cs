using EvChargers.Domain.Entities;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Common.Mapping;

public static class StationMappings
{
    public static StationListItemDto ToListItemDto(this Station s) =>
        new(s.Id, s.Name, s.Location.Y, s.Location.X, s.Status.ToString(),
            s.Reviews.Count > 0 ? s.Reviews.Average(r => r.Rating) : null);

    public static StationDetailDto ToDetailDto(this Station s) =>
        new(s.Id, s.Name, s.Address, s.Location.Y, s.Location.X, s.Status.ToString(),
            s.Operator?.Name,
            s.Connectors.Select(c => new ConnectorDto(c.Type.ToString(), c.PowerKw, c.Count)).ToList(),
            s.Reviews.Count > 0 ? s.Reviews.Average(r => r.Rating) : null,
            s.Reviews.Count);
}