namespace Edument.CQRS;

using System.Collections;

public interface IEventStore
{
    IEnumerable LoadEventsFor<TAggregate>(Guid id);
    void SaveEventsFor<TAggregate>(Guid id, int eventsLoaded, ArrayList newEvents);
}