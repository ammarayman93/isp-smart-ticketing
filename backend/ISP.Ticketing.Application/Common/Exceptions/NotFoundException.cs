namespace ISP.Ticketing.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException() : base() { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string name, object key)
        : base($"«·ﬂÌ«‰ \"{name}\" ({key}) €Ì— „ÊÃÊœ.") { }
}