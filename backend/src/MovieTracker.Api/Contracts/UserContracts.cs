namespace MovieTracker.Api.Contracts;

public sealed record UserResponse(Guid Id, string Email, string Name, string? PictureUrl);
