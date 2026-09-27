namespace EvChargers.Application.Common;

/// <summary>Outcome of a write operation, mapped by controllers to 204 / 404 / 403.</summary>
public enum OperationResult { Ok, NotFound, Forbidden }

/// <summary>Outcome of posting a review, mapped by controllers to 201 / 200 / 404.</summary>
public enum ReviewUpsertResult { Created, Updated, StationNotFound }
