namespace ISP.Ticketing.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("ÕœÀ Œÿ√ Ê«Õœ √Ê √ﬂÀ— √À‰«¡ «· Õﬁﬁ „‰ «·»Ì«‰« ")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : this()
    {
        Errors = errors;
    }
}