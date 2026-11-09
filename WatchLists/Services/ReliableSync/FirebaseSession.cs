namespace WatchLists.Services.ReliableSync;

public sealed record FirebaseSession(string UserId, string IdToken, string RefreshToken);
