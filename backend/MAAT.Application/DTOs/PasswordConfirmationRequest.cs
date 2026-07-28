namespace MAAT.Application.DTOs;

// Payload partagé par POST /api/me/export et DELETE /api/me (section 6) : les deux
// exigent une reconfirmation du mot de passe, dans le corps JSON. Jamais en en-tête
// ni en query string, qui finiraient dans les journaux des reverse proxies (section 5).
public sealed record PasswordConfirmationRequest(string Password);
