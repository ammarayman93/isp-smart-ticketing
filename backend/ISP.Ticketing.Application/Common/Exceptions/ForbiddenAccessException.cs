namespace ISP.Ticketing.Application.Common.Exceptions;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("·Ì” ·œÌﬂ ’·«ÕÌ… ··Ê’Ê· ≈·Ï Â–« «·„Ê—œ") { }
    public ForbiddenAccessException(string message) : base(message) { }
}