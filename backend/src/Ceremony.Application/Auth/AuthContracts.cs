namespace Ceremony.Application.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, LoginUser User);

public sealed record LoginUser(int Id, string Username, string? Name);
