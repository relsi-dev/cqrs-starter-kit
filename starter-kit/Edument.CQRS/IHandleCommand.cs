namespace Edument.CQRS;

using System.Collections;

public interface IHandleCommand<TCommand>
{
    IEnumerable Handle(TCommand c);
}