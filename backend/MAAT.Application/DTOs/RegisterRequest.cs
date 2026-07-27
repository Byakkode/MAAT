using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string CompanyName,
    string SectorCode,
    CompanySizeRange SizeRange,
    string Region,
    string? Siret);
