namespace LearningLms.SharedKernel;

/// <summary>Marker for something that happened in a module's Domain layer. Modules publish
/// these internally; nothing here implies a messaging framework — how they're dispatched is
/// each module's own Infrastructure concern.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
