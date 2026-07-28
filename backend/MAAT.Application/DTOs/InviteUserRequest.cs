using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

public sealed record InviteUserRequest(string Email, UserRole Role);
