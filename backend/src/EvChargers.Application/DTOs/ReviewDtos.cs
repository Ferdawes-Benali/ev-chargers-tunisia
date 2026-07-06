namespace EvChargers.Application.DTOs;

public record CreateReviewRequest(int Rating, string? Comment);
public record ReviewDto(Guid Id, int Rating, string? Comment, DateTime CreatedAt);