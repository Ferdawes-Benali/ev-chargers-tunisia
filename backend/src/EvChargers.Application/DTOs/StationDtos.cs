namespace EvChargers.Application.DTOs;

public record StationListItemDto(Guid Id, string Name, double Lat, double Lng,
                                 string Status, double? AvgRating);

public record StationDetailDto(Guid Id, string Name, string? Address, double Lat, double Lng,
                               string Status, string? OperatorName,
                               List<ConnectorDto> Connectors, double? AvgRating, int ReviewCount);

public record ConnectorDto(string Type, int PowerKw, int Count);

public record CreateStationRequest(string Name, string? Address, double Lat, double Lng,
                                   Guid? OperatorId, List<ConnectorDto> Connectors);