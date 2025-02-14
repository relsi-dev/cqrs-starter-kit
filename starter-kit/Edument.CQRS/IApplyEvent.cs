﻿namespace Edument.CQRS;

/// <summary>
/// Implemented by an aggregate once for each event type it can apply.
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public interface IApplyEvent<TEvent>
{
    void Apply(TEvent e);
}